---
name: unity-runtime-bug-hunt
description: Unity Editor PlayMode 中のランタイム挙動不具合・例外・応答不能の原因特定スキル。uloop execute-dynamic-code でランタイム状態を広く浅くダンプし、JetBrains Rider debugger で狭く深く変数/BPヒットを観測する。TRIGGER when: (1) ユーザーのメッセージに PlayMode 実行時のランタイム例外スタックトレースが貼られている（NullReferenceException / TimeoutException / IndexOutOfRangeException 等）(2) 「〇〇するとエラー/例外が出る」「破壊/生成したら固まった」などプレイ操作と異常の因果が絡む (3) クライアント-サーバー通信のタイムアウト・応答 null・"Receive null"・パケット未返信・サーバー固まり (4) 「動かない」「反応しない」「呼ばれない」「途中で止まる」「期待した動きにならない」「なぜ〇〇にならない？」といった実行フロー系 (5) 「ランタイム状態/実行中のインスタンスを見て」「実行中のコンポーネント状態を確認」 (6) null の直接源は分かるが、なぜその値が null かがランタイム状態依存（別スレッド/サーバー側/非同期/他インスタンス）。ユーザーが "debug" / "breakpoint" を明示しなくても上記いずれかに該当すれば起動する。SKIP when: (a) コンパイルエラーや型エラー（静的バグ） (b) null の原因が静的初期化漏れと即特定できる（フィールド未初期化がソース読むだけで自明等） (c) テスト入力値の誤りなどコード読解だけで完結するケース。迷ったら起動する（起動コスト低、起動漏れコスト高）。
---

# unity-runtime-bug-hunt

## 前提条件

- Unity Editor がPlayModeで起動中、かつJetBrains RiderがUnityプロセスにアタッチ済み（`mcp__rider-debugger__list_debug_sessions` で `state: running` のセッションが見えること）
- `uloop` CLI が install 済み（`uloop --version` で確認、無ければ対象プロジェクトの README に従って install）、対象プロジェクトに `--project-path` でアクセス可能
- 対象コードのクラス名・namespace・フィールド名を `Grep` / `Read` で直接確認できる前提（= Debugger の実行時型情報だけに頼らない）

## 基本原則

**ツールの役割を混同しない。** 混同すると Unity が busy 化したり、存在しないバグを追って時間を溶かす。

| 問い | 使うツール | 理由 |
|---|---|---|
| 今どんなインスタンスがどこに存在するか？ | `execute-dynamic-code` | コレクション全走査が1コールで済む |
| 各インスタンスのフィールド値・インベントリ・参照関係は？ | `execute-dynamic-code` | property getter / LINQ / 拡張メソッドが全部使える |
| このメソッドは呼ばれているか？ | Rider BP | `hitCount` が決定的証拠 |
| この時点での引数・ローカル変数は何？ | Rider `get_variables` / `evaluate_expression` | そのフレーム内の値のみ |
| 期待したスレッド（更新ループ・ワーカー等）は生きているか？ | Rider `list_threads` | スレッド名で存在確認 |
| 例外は出ていないか？ | `uloop get-logs --log-type Error/Exception` | ログを先に見ると近道 |

**黄金律:** 動的コードでランタイム状態の真実を先に取り、**その後で** debugger を「ピンポイント観測」として使う。逆は時間の無駄。

## 手順

### Step 1. 症状と期待動作を一文化する

「〇〇がXXするはずなのにYYする」の形で書き出す。この一文がそのまま Step 3 の dynamic code で確認する項目リストになる。あいまいだと以降の Step が全部ブレるので、ここで精度を出す。

### Step 2. uloop get-logs で既出エラーを回収する

```bash
uloop get-logs --project-path ./{project} --log-type Error --max-count 30
uloop get-logs --project-path ./{project} --log-type Exception --max-count 30
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

### Step 4. Debugger BP で「呼ばれているか」を確認する

状態は正しいのに挙動が間違っている場合は、コードパスが本当に走っているかを確認する。状態が正しい=bug は **実行フロー側** にある。

1. 疑わしいメソッドの先頭行に BP を設置
2. `wait_for_pause` でタイムアウト（例: 15秒）を指定して待つ
3. ヒットすれば実行中 → Step 5。ヒットしなければ上流（初期化、イベント購読、スレッド起動）を疑う

**「BPが当たらない = そこに辿り着いていない」は強い証拠。** 逆にBP hitCount=0が数十秒続くなら、上流で呼び出し自体が止まっている。

BP運用の詳細・コツ・失敗パターンは [references/debugger-gotchas.md](references/debugger-gotchas.md) 参照。

### Step 5. Debugger で変数を観測する

BP が当たったら `get_variables` でローカル変数一覧を取得。個別値は `evaluate_expression` で見る。

**重要な制約: Riderの soft debugger は "Implicit evaluation is disabled" がデフォルト。** property getter と method call は動かない。フィールド直アクセスが必要:

```csharp
// 動かない例
someList.Count                     // property getter
someDict[key]                      // indexer（property）
obj.ComputeSomething()             // method call

// 動く例
someList._size                     // List<T>.Count の実体
someList._items[i]                 // List<T> の内部配列
obj._privateField                  // 直接フィールドアクセス
```

`List<T>` は `_items` (T[]) と `_size` (int) がランタイムフィールド。property `Count` は使えない。配列インデクサ `[i]` はフィールド経由 (`_items[i]`) なら効く。

型判定で `is` を使うときは **fully qualified name** が必須:

```csharp
// NG: The type or namespace name 'XXX' does not exist
_items[i] is ItemStack

// OK
_items[i] is Core.Item.Implementation.ItemStack
```

**internal クラスでも namespace.Class まで書けば参照できる。** エラーメッセージが出たら `Grep` で namespace を確認して付け直す。

### Step 6. 毎回必ず後片付けする

以下を **必ず** 実行する。残すと次回セッションで誤ヒット・誤診断・Unity freeze を引き起こす。

1. `mcp__rider-debugger__list_breakpoints` で自分が張った line BP を列挙
2. `remove_breakpoint` で全削除（exception BP は残してOK）
3. セッションが `paused`（特に `pausedReason: step`）で止まっていたら `resume_execution`
4. 作業完了を宣言

## Gotchas

### G1: Unity が busy になったら即 Rider debugger セッションを停止する

**症状:** `uloop execute-dynamic-code` が 180秒タイムアウトでエラー、Unity Editor が応答しない、`get-logs` も返ってこない。

**原因:** Rider debugger が paused/step 状態で止まったまま離脱されると、Unity のスクリプトスレッドが debugger のコマンド待ちで固まる。特に tracepoint/logpoint で複雑な `{this}` 式を評価させると頻発する。

**対処:** **Rider側でdebugger停止ボタンを押す** (Shift+F5) → Unity が即座に復活する。MCP経由で`stop_debug_session`を呼んでもよい。

**予防:**
- tracepoint/logpoint には複雑な式（`{this}` やフィールド連鎖）を入れない。`i={i}` 程度にする
- 長時間 Unity に触れない時は BP を外しておく
- 作業が終わったら Step 6 のクリーンアップを忠実に実行

### G2: 「Implicit evaluation is disabled」は debugger 側の制限。dynamic code では全機能使える

Debugger の `evaluate_expression` / 条件付きBP では property getter やメソッドが呼べないが、`uloop execute-dynamic-code` は通常のC#実行環境なので **LINQ・property・拡張メソッド・await すべて使える**。複雑な集計は debugger でがんばらず動的コードに移すと速い。

### G3: 静的state は PlayMode 停止後も残ることがある

Unity Editor の Domain Reload を無効にしている場合、静的フィールド（例: シングルトン、サービスプロバイダ、UniRx Subject）は PlayMode 停止→再開後も前回の状態を引きずる。「さっき見たインスタンスが再開後にも見える」のは新しくロードされたのではなく残骸の可能性がある。

**確認法:** PlayMode 再開後に `list_threads` で期待するバックグラウンドスレッドが**新しく**存在しているか、プロジェクト特有の初期化ログが出ているかを見る。

### G4: BPのhitCount=0は「通っていない」の強い証拠

`list_breakpoints` は各line BPのhitCountを返す。数十秒PlayMode継続後にhitCount=0なら、そのコードは実行されていない。**「BPの設定漏れ？」と疑う前にhitCountを信じる。** 逆に、hitCountが増えているのにwait_for_pauseがタイムアウトするなら、suspend policyが`none`（tracepoint）になっているか、別スレッドでヒットしている。

### G5: 複数インスタンスへのBPは「全ヒット」する

同じクラスのインスタンスが N 個ある場合、そのクラスのメソッドに張った BP は N 個全部でヒットする。「対象のインスタンスだけ見たい」時は Step 3 の動的コードダンプでインスタンスID（`BlockInstanceId` や `GetInstanceID()` 等）を先に特定し、BPの条件に `this._instanceId == ...` を入れる。

### G6: for ループで empty 要素を早期 skip する条件があると BP body は発火しない

例: 「全スロット舐めるループだが、空アイテムは `continue` / ループ条件で skip」というパターン。外側のループ先頭BPは当たるが、内部処理のBPは永久に当たらない。「呼ばれているはず」なのに当たらない時は **外側ループの条件** を読んで早期 skip がないか確認する。

### G7: 条件付きBP の式は最初に `i == 0` 等で syntax 検証する

いきなり複雑な条件（`_items[i] is My.Ns.Class`）を書くと、式エラーで **silent に BP が無効化** されることがある（Rider の場合ダイアログで "ブレークポイントで停止しますか？" が出る）。まず確実に true になる条件（`i == 0` 等）で一度ヒットさせ、その後にステップで `evaluate_expression` しながら本条件を組み立てると確実。

### G8: field 名や型名からアーキテクチャ構成を推論するな。必ず probe で確定

**症状:** `_localServerProcess (Process)` のような field を見て「サーバーは別OSプロセスだから見えない」と判断し、動的コードを試さずに「届かない」と結論。実際には PlayMode 中は同一プロセスで `ServerContext.*` が普通に引ける構成だった。

**原因:** field 名・型名は**デプロイモードごとに意味が変わる**。`Process` 型の field があっても「製品ビルドでサブプロセス起動する時専用で、PlayMode では未使用」というケースがある。名前だけ見て結論すると、実測が1コールで済むはずのアクセス可能性を誤判定する。

**対処:** 「この状態は見えない / この API は届かない」と言う前に、**uloop execute-dynamic-code で該当エントリーポイントを1回叩く**。返ったなら見える、throw / null なら見えない。1コールで確定する。

```csharp
// アクセス可能性の probe テンプレ
var ctx = Game.Context.ServerContext.WorldBlockDatastore;
return ctx == null ? "null" : $"OK count={ctx.BlockMasterDictionary.Count}";
```

`ServerContext` / `ClientContext` / 静的 singleton など、「見えるかどうか」が構成依存の対象は **プロジェクトごとに [references/project-api-cheatsheet.md](references/project-api-cheatsheet.md) に in-process 可否を記載する**。未登録なら probe してから追記する。

**黄金律の再掲:** 「見えない」は推論でなく probe の結果として宣言する。推論で諦めると、余計なインストゥルメント提案や別 Unity 起動提案で時間を溶かす。

## 典型的な調査フロー例

**症状:** 「プレイヤーが拾えるはずのアイテムが表示されない」

1. **Step 2 ログ確認:** runtime Exception なし → コード例外ではない
2. **Step 3 動的コード:** ワールドの全 Item インスタンスを ID / pos / owner でダンプ
   - 発見: Item は存在するが `pos` がプレイヤーの遥か遠くにあった
   - → **原因: spawn ロジックの座標計算ミス**（ここで判明、Step 4 不要）

**症状:** 「オブジェクトの Update() が一切呼ばれない」

1. **Step 2:** Exception なし
2. **Step 3:** 対象オブジェクトはコレクションに存在する（= 生成自体は成功）
3. **Step 4:** 該当 `Update` に BP → **hitCount=0**
4. 上流へ: 購読元（`GameUpdater.Update` 等）に BP → **hitCount=0**
5. さらに上流: 更新スレッドのエントリに BP → **hitCount=0**
6. `list_threads` → 期待する更新スレッドが存在しない
7. → **原因: initialization pipeline が更新ループを起動していない** (コードを Read で確認)

両方とも **Step 3 の動的コードスナップショット→ BP hitCount を上流へ追う** のパターンで特定している。
