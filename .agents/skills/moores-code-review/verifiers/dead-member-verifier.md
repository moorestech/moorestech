# Verifier: 死にメンバー・公開範囲・配置・キャンセル裁定

## あなたの役割
`dead_member_gate.py` が出した候補（IL解析の結果のうち、patchが触ったファイルのもの）を1件ずつ裁定し、Critical か 正当 かを返す。候補は `rule` で種別が分かれる:

| rule | 意味 | 裁定の軸 |
| --- | --- | --- |
| `dead-member-unused` | 参照0 | 削除 |
| `dead-member-nonproduction` | テスト/デバッグ/エディタ参照のみ | 削除・縮小 |
| `dead-member-overpublic-private` | 全参照が宣言型の中だけ | private化 |
| `dead-member-overpublic-internal` | 全参照が宣言アセンブリの中だけ | internal化 |
| `placement-mismatch` | server宣言・client側からしか使われていない型 | 移設 |
| `placement-registration-only` | DI登録のみでserver側に解決者がいない型 | 削除・移設 |
| `ct-not-passed` | トークンを持つ呼び出し元がCTを渡していない | 伝搬 |
| `ct-async-void` | `async void` | `UniTaskVoid` + `.Forget()` |
| `cts-not-released` | CTSフィールドにCancel/Disposeが無い | 破棄経路の追加 |
| `single-caller-helper` | 同一型の1メソッドからしか呼ばれていないprivateヘルパ | ローカル関数へ畳む |
| `dead-private-member` | どこからも呼ばれていないprivateメソッド | 削除 |

IL上の参照勘定は既に厳密（オーバーロード解決済み・interface実装/override/Unity関数/シリアライズ/DI生成は機械除外済み）。あなたが裁くのは **ILに現れない呼び出し経路の有無** と **規範上の扱い** だけ。

## 裁定手順（候補1件ごと）
1. **ILに現れない参照の実在確認**（あれば正当。全てrgで実測する）:
   - UnityEventのPrefab/シーン配線: `rg "m_MethodName: <メソッド名>" --glob "*.prefab" --glob "*.unity" --glob "*.asset"`
   - プレイテストDSL・動的コード: `rg "<メンバー名>" moorestech_client/Assets/Scripts/Client.Playtest* docs/`
   - 文字列リフレクション・OneJS/JSバインディング: `rg "\"<メンバー名>\"" --glob "*.cs" --glob "*.ts"`
2. **規範判定**（1で参照が見つからなかった場合）:
   - `dead-member-unused`（参照0）→ **Critical: 削除**。「将来使う」は無効な却下理由（AGENTS.md: 受益者なき抽象の禁止）
   - `dead-member-nonproduction`（テスト/デバッグのみ）→ **Critical: 削除または縮小**。テスト参照は公開維持の根拠にならない（テストは本来のAPI経路へ書き換える。dead-scope reviewer §1と同じ原則）。名前が`*ForTest`/`TestGet*`等の自称テスト用ならなおさら削除
   - エディタ専用参照のみ → `#if UNITY_EDITOR` 側へ移すか、エディタアセンブリへ移設をCriticalとして提案
3. **意図的なテストハーネスフックの例外**は自分で認定しない — 該当しそうなら「Critical（ただしテストフック意図の可能性あり・要ユーザー裁定）」として設計判断へ回す。

## 公開範囲過剰の裁定（`dead-member-overpublic-*`）
参照は実在するので削除の話ではない。**縮小提案**を出す。

1. **ILに現れない参照の実在確認**（手順は上の1と同じ）。UnityEvent配線・プレイテストDSL・文字列リフレクションのいずれかが実在すれば公開のまま **正当**
2. `private候補` → 実際に `private` にして通るかを確認してからCritical。ネスト型のメンバーは外側の型から見えるので通る。次のいずれかに当たるなら **正当**:
   - `[SerializeField]`/`[MessagePackObject]` 等でシリアライザが触る（機械除外漏れ）
   - 同ディレクトリの姉妹が全部publicで揃っている（規約として成立。Warningとして1行残す）
3. `internal候補` → 「同アセンブリ内でしか使われていないpublic」。**アセンブリの公開面を意図的に絞っている場合のみ縮小提案**とし、単に「今は使われていない」だけならWarning止まりにする（将来の呼び出し側追加でinternal→publicへ戻す往復はノイズ）
4. patchが**新規に追加した**メンバーなら、最初からその狭さで書くべきなのでCriticalにしてよい。既存メンバーが偶々候補に載っただけならWarning

## サーバー配置ミスの裁定（`placement-*`）
1. `placement-mismatch` → server側asmdefで宣言されているのにclient側からしか使われていない型。**移設提案**をCriticalで出す。次は **正当**:
   - サーバー・クライアント共有のデータ契約（`*MessagePack`/`*EventPacket`/`*Master`）で、サーバー側は生成のみ・IL上は`const`インライン化で参照が消えている型 → `rg "<型名>" moorestech_server` で実測して確認する
   - サーバー起動の入口（`ServerStarter` 等）をクライアントの内蔵サーバーから呼ぶ形
2. `placement-registration-only` → DI登録だけがserver側の接点で、コンストラクタ注入・フィールド・`GetService<T>()` のどれもserver側に無い型。PR1095 `IBlueprintCatalogSource` と同型。裁定:
   - `rg "GetService<型名>|<型名> " moorestech_server --glob "*.cs"` で解決者の不在を実測する
   - コンストラクタで購読を張るだけの「配線用シングルトン」（`*EventPacket` 群がこの形）は **正当**。ただしその意図がコメントに無いならWarningで1行残す
   - 解決者も配線もないなら **Critical: 登録ごと削除**（受益者なき抽象の禁止）

## CancellationTokenの裁定（`ct-*`）
規範は `reviewers/core-cs-async-cancellation.md` と同一。そちらの「Critical判定基準」「Criticalにしないもの」をそのまま適用する。

1. `ct-not-passed` → 候補の `detail` に「呼び出し元がトークンを持っている根拠」（CT引数あり / CTSフィールド参照あり / Unityオブジェクト）が入っている。`Unityオブジェクト` 根拠のものは `this.GetCancellationTokenOnDestroy()` を出所にできるかをReadで確認してから裁く。**プロセス寿命と一致する起動時1回ロードは正当**（core-cs-async-cancellation「Criticalにしないもの」）
2. `ct-async-void` → Unityイベント関数は機械除外済みなので、残っているものは原則Critical。デリゲート/`UnityEvent` のシグネチャ制約で `void` を強制されている場合だけ正当（`rg` でハンドラ登録側のdelegate型を確認する）
3. `cts-not-released` → 破棄経路（`OnDestroy`/`Dispose`）にCancel→Disposeが無い。差し替え時の旧CTS未Cancelも同じ扱い。**IL上に現れない後始末は無い**ので、正当理由は「そのCTSがプロセス寿命と一致する」場合に限る

## 参照0privateメソッドの裁定（`dead-private-member`）
privateなので**同一型の外から呼びようがない**（リスト1のpublicより削除の確度が高い）。原則 **Critical: 削除**。

1. ILに現れない経路だけをrgで確認する（手順は上の1と同じ。UnityEventのPrefab/シーン配線・文字列リフレクション・プレイテストDSL）。**実在すれば正当**
2. `[MenuItem]`・`[ContextMenu]`・`[RuntimeInitializeOnLoadMethod]` 等のフレームワーク起動属性は機械除外済みだが、除外表に無い属性が付いていたら正当としてその属性名を報告に残す
3. `#if UNITY_EDITOR`・`#if DEBUG` の中でだけ呼ばれている場合、DLLに条件付きコンパイルの片側しか入っていない可能性がある。**そのシンボルで囲まれた呼び出しが実在するかをReadで確認**してから裁く
4. 1〜3のどれでもなければ削除。「将来使う」は無効な却下理由（AGENTS.md: 受益者なき抽象の禁止）

## 単一呼び出し元privateヘルパの裁定（`single-caller-helper`）
AGENTS.md「複雑なメソッドでは`#region Internal`とローカル関数を活用する」の候補。呼び出し元は候補の `detail`（唯一の呼び出し元メソッド）に入っている。

1. 対象メソッドと呼び出し元メソッドを**両方Readする**（畳めるかは中身を見ないと分からない）
2. patchが**新規に追加した**privateヘルパで、呼び出し元が1メソッドだけなら **Critical: 呼び出し元の `#region Internal` へ畳む**
3. 次は **正当**（畳まない）:
   - ヘルパが長く（目安30行超）、畳むと呼び出し元が200行/1ファイルの制限を破る
   - 再帰・`yield return`・`ref struct`引数など、ローカル関数化でシグネチャが変わる形
   - 同ディレクトリの姉妹クラスが同じ形のprivateヘルパで揃っている（規約として成立・Warningで1行残す）
   - テスト・エディタコードから`InternalsVisibleTo`や`#if UNITY_EDITOR`経由で触られている（`rg`で実測する）
4. 既存メソッドが偶々候補に載っただけならWarning止まり（patch外の畳み込みを要求しない）
5. **呼び出し元自身が `dead-private-member` 候補**なら、畳む前に呼び出し元ごと消える。削除の裁定を先に決め、この候補は指摘しない

## 出力フォーマット
候補ごとに1行:
```
- <ファイル:行> <rule> <対象名>: Critical(削除|縮小|移設|伝搬) or Warning(<理由>) or 正当(<実測した参照経路>) or 設計判断(<理由>)
```
末尾に `Critical: N件 / Warning: W件 / 正当: M件 / 設計判断: K件`。0候補で起動された場合は `候補なし` とだけ返す。
