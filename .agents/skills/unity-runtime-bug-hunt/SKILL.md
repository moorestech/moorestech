---
name: unity-runtime-bug-hunt
description: >-
  Unity Editor PlayMode 中のランタイム挙動不具合・例外・応答不能の原因特定スキル。uloop execute-dynamic-code でランタイム状態をダンプし、uloop get-logs と一時ログで実行フローを確定する。TRIGGER when: (1) ユーザーのメッセージに PlayMode 実行時のランタイム例外スタックトレースが貼られている（NullReferenceException / TimeoutException / IndexOutOfRangeException 等）(2) 「〇〇するとエラー/例外が出る」「破壊/生成したら固まった」などプレイ操作と異常の因果が絡む (3) クライアント-サーバー通信のタイムアウト・応答 null・"Receive null"・パケット未返信・サーバー固まり (4) 「動かない」「反応しない」「呼ばれない」「途中で止まる」「期待した動きにならない」「なぜ〇〇にならない？」といった実行フロー系 (5) 「ランタイム状態/実行中のインスタンスを見て」「実行中のコンポーネント状態を確認」 (6) null の直接源は分かるが、なぜその値が null かがランタイム状態依存（別スレッド/サーバー側/非同期/他インスタンス）。ユーザーが "debug" を明示しなくても上記いずれかに該当すれば起動する。SKIP when: (a) コンパイルエラーや型エラー（静的バグ） (b) null の原因が静的初期化漏れと即特定できる（フィールド未初期化がソース読むだけで自明等） (c) テスト入力値の誤りなどコード読解だけで完結するケース。迷ったら起動する（起動コスト低、起動漏れコスト高）。
---

# unity-runtime-bug-hunt

## 前提条件

- Unity Editor がPlayModeで起動中（作業worktreeのEditor。`uloop` が疎通すること）
- `uloop` CLI が install 済み（`uloop --version` で確認、無ければ対象プロジェクトの README に従って install）、対象プロジェクトに `--project-path` でアクセス可能
- 対象コードのクラス名・namespace・フィールド名を `Grep` / `Read` で直接確認できる前提

## 基本原則

**ツールの役割を混同しない。** 混同すると Unity が busy 化したり、存在しないバグを追って時間を溶かす。

| 問い | 使うツール | 理由 |
|---|---|---|
| 今どんなインスタンスがどこに存在するか？ | `execute-dynamic-code` | コレクション全走査が1コールで済む |
| 各インスタンスのフィールド値・インベントリ・参照関係は？ | `execute-dynamic-code` | property getter / LINQ / 拡張メソッドが全部使える |
| このメソッドは呼ばれているか？ | 一時 `Debug.Log` ＋ `uloop get-logs` | 出ない＝通っていない、が決定的証拠 |
| 更新ループは生きているか？ | `execute-dynamic-code` で `GameUpdater.CurrentTick` を2回読む | 進んでいなければ更新スレッド未起動 |
| 例外は出ていないか？ | `uloop get-logs --log-type Error`（例外は Error 型に含まれる） | ログを先に見ると近道 |

**黄金律:** 動的コードでランタイム状態の真実を先に取り、**その後で** 一時ログによる実行フロー確認へ進む。逆は時間の無駄。

**再現状態の構築**: バグ再現に「PlayMode起動＋足場＋ブロック設置＋アイテム付与」等の定型準備が要る場合、`Client.Playtest` があるブランチではプレイテストDSL（`unity-playmode-recorded-playtest` スキル・方式A）で1コマンド構築できる。手で uloop を往復して状態を作らない。

## 手順

### Step 1. 症状と期待動作を一文化する

「〇〇がXXするはずなのにYYする」の形で書き出す。この一文がそのまま Step 3 の dynamic code で確認する項目リストになる。あいまいだと以降の Step が全部ブレるので、ここで精度を出す。

### Step 2. uloop get-logs で既出エラーを回収する

```bash
uloop get-logs --project-path ./{project} --log-type Error --max-count 30
```

ランタイム例外が出ていれば90%ここで原因がわかる。`DynamicCommand_*.dll` のコンパイルエラーは自分の過去の動的コードの残骸なので無視してよい。

### Step 3. 動的コードでランタイム状態をスナップショット

**これが本スキルの中核。** Step 1 で書いた一文の全名詞（対象オブジェクト・状態・関係性）を dynamic code で列挙・ダンプして、**「何が存在していて、どうなっているか」を文字列一発で把握する**。

最小テンプレート（プロジェクト非依存の汎用パターン）:

```csharp
using System.Linq;
using System.Text;

var sb = new StringBuilder();

// 1. エントリーポイントから対象コレクションを取得
var items = SomeStaticContext.GetAll();   // ← 対象PJT固有の呼び出しに置換

// 2. 各インスタンスのID・位置・疑わしいフィールドをダンプ
foreach (var x in items)
{
    sb.AppendLine($"[Id={x.Id}] state={x.State} connectedCount={x.Connected.Count}");

    // 3. 内部状態を条件付きで詳細ダンプ
    if (x.State == TargetState)
        foreach (var c in x.Connected)
            sb.AppendLine($"  -> {c.GetType().Name}");
}

sb.AppendLine($"Total={items.Count}");
return sb.ToString();
```

**ポイント:**
- 1回のコールで **全対象** を列挙する。個別クエリを何度も叩かない
- ID・位置・疑わしいフィールドをまとめて1行で出す（`$"[Id=... pos=... count=..."`）
- 条件分岐で詳細情報を追加（空インスタンスはスキップ、非空のみ内部ダンプ等）

プロジェクト固有の**エントリーポイント**（どの static から世界を取るか）、**主要コレクション名**、**ありがちな名前ミス**、よく使うコンポーネント取得API は、実運用時に使うプロジェクトごとに [references/project-api-cheatsheet.md](references/project-api-cheatsheet.md) に追記して使い回す。moorestech 系の例（`ServerContext.WorldBlockDatastore` 起点、`block.ComponentManager.TryGetComponent<T>`、`BlockPositionInfo.OriginalPos` 等）は既に記載済み。

**Step 3 完了判定:** 取得した状態が Step 1 の期待と**どこで食い違っているか**を特定する。多くの場合はここで原因が判明する（例: 「状態は入っているが参照先のコレクションが空」）。状態に異常がなければ Step 4 へ。

### Step 4. コード修正・一時ログ挿入の前に PlayMode を停止する

`.cs` / `.asmdef` を Edit/Write する直前に**必ず** PlayMode を停止する（再度ランタイム観測だけする場合は不要）:

```bash
uloop control-play-mode --action stop --project-path ./{project}
```

**理由:** PlayMode 中に `.cs` を保存すると Unity は scripts compiled in PlayMode に入り、走行中の世界のオブジェクトは古いクラス定義のまま動き続ける（Domain Reload 無効なら永久にすり替わらない）。他コンポーネントを `is` / `as` で型判定して接続する機構（コネクタ・インベントリ等）が壊滅的に壊れ、切り分けに数十分かかる。ユーザーが手動で PlayMode を回している時も、編集に入る瞬間に「PlayMode を停止します」と一言入れて停止する。

### Step 5. 一時ログで「呼ばれているか」を確定する

状態は正しいのに挙動が間違っている場合は、コードパスが本当に走っているかを確認する。状態が正しい=bug は **実行フロー側** にある。

1. Step 4 で PlayMode を停止し、疑わしいメソッドの先頭に `Debug.Log("[hunt] <メソッド名> <主要引数>")` を1行入れる（プレフィックス `[hunt]` で後から grep・除去できるようにする）
2. `uloop compile` → PlayMode 再開 → 再現操作（プレイテストDSLがあればシナリオで）
3. `uloop get-logs --project-path ./{project} --log-type Log --search-text "[hunt]"` で出たか見る

**「ログが出ない = そこに辿り着いていない」は強い証拠。** 出ないなら呼び出し元へ遡って同じログを足し、「どこまでは動いているか」の境界を確定する（推論より経験的に正確）。同じクラスのインスタンスが N 個あるとログも N 個出るので、Step 3 で特定した ID を必ずログに含める。

**引数・ローカル変数の値**も同じログに `$"..."` で載せて取る。複雑な集計はログでがんばらず、Step 3 の `execute-dynamic-code`（通常のC#環境。LINQ・property・await が全部使える）へ移す。

### Step 6. 毎回必ず後片付けする

`[hunt]` ログは調査用であり、プロダクションに残さない（AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」と同じ扱い）。

1. `grep -rn "\[hunt\]" --include=*.cs` で自分が入れたログを列挙し全て除去する
2. `uloop compile` を通す
3. 作業完了を宣言

## Gotchas

### G1: 静的state は PlayMode 停止後も残ることがある

Unity Editor の Domain Reload を無効にしている場合、静的フィールド（例: シングルトン、サービスプロバイダ、UniRx Subject）は PlayMode 停止→再開後も前回の状態を引きずる。「さっき見たインスタンスが再開後にも見える」のは新しくロードされたのではなく残骸の可能性がある。

**確認法:** PlayMode 再開後にプロジェクト特有の初期化ログが**新しく**出ているか、`GameUpdater.CurrentTick` がリセットされているかを見る。

### G2: 複数インスタンスのログは「全部出る」

同じクラスのインスタンスが N 個ある場合、そのメソッドに入れた一時ログは N 個全部から出る。「対象のインスタンスだけ見たい」時は Step 3 の動的コードダンプでインスタンスID（`BlockInstanceId` や `GetInstanceID()` 等）を先に特定し、ログ本文に ID を含めて grep で絞る。

### G3: for ループで empty 要素を早期 skip する条件があると内部のログは出ない

例: 「全スロット舐めるループだが、空アイテムは `continue` / ループ条件で skip」というパターン。外側のループ先頭のログは出るが、内部処理のログは永久に出ない。「呼ばれているはず」なのに出ない時は **外側ループの条件** を読んで早期 skip がないか確認する。

### G4: field 名や型名からアーキテクチャ構成を推論するな。必ず probe で確定

**症状:** `_localServerProcess (Process)` のような field を見て「サーバーは別OSプロセスだから見えない」と判断し、動的コードを試さずに「届かない」と結論。実際には PlayMode 中は同一プロセスで `ServerContext.*` が普通に引ける構成だった。

**原因:** field 名・型名は**デプロイモードごとに意味が変わる**。`Process` 型の field があっても「製品ビルドでサブプロセス起動する時専用で、PlayMode では未使用」というケースがある。名前だけ見て結論すると、実測が1コールで済むはずのアクセス可能性を誤判定する。

**対処:** 「この状態は見えない / この API は届かない」と言う前に、**uloop execute-dynamic-code で該当エントリーポイントを1回叩く**。返ったなら見える、throw / null なら見えない。1コールで確定する。

```csharp
// アクセス可能性の probe テンプレ
var ctx = Game.Context.ServerContext.WorldBlockDatastore;
return ctx == null ? "null" : $"OK count={ctx.BlockMasterDictionary.Count}";
```

`ServerContext` / `ClientContext` / 静的 singleton など、「見えるかどうか」が構成依存の対象は **プロジェクトごとに [references/project-api-cheatsheet.md](references/project-api-cheatsheet.md) に in-process 可否を記載する**。未登録なら probe してから追記する。

**黄金律の再掲:** 「見えない」は推論でなく probe の結果として宣言する。推論で諦めると、別 Unity 起動提案などで時間を溶かす。

## 典型的な調査フロー例

**症状:** 「プレイヤーが拾えるはずのアイテムが表示されない」

1. **Step 2 ログ確認:** runtime Exception なし → コード例外ではない
2. **Step 3 動的コード:** ワールドの全 Item インスタンスを ID / pos / owner でダンプ
   - 発見: Item は存在するが `pos` がプレイヤーの遥か遠くにあった
   - → **原因: spawn ロジックの座標計算ミス**（ここで判明、Step 5 不要）

**症状:** 「オブジェクトの Update() が一切呼ばれない」

1. **Step 2:** Exception なし
2. **Step 3:** 対象オブジェクトはコレクションに存在する（= 生成自体は成功）
3. **Step 5:** 該当 `Update` に `[hunt]` ログ → **出ない**
4. 上流へ: 購読元（`GameUpdater.Update` 等）にログ → **出ない**
5. `execute-dynamic-code` で `GameUpdater.CurrentTick` を2回読む → 進んでいない
6. → **原因: initialization pipeline が更新ループを起動していない** (コードを Read で確認)

両方とも **Step 3 の動的コードスナップショット→ 一時ログを上流へ追う** のパターンで特定している。
