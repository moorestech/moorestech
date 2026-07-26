# 電線接続パラメータinterface化＋自動接続選定コア共通化 レビュー記録 (2026-07-26)

## 対象
- base: `13ec693b0` / reviewed head: `bf00914a9`（レビュー時点）→ 修正適用後 `e76c5de08`
- ブランチ: `feature/fix-eletric-connect`（worktree tree1）/ PR: #1057 のセルフレビュー2指摘への対応
- context要約
  - ゴール: ①blocks.ymlの電気系8種の3キーをスキーマinterfaceでC#側から一括処理（resolver 9分岐→3分岐）②自動接続の候補選定を純粋コアへ抽出しサーバー/クライアントを薄いアダプタ化
  - 非目標: yaml上の3キー×8箇所の重複解消（ユーザー裁定「あるべき姿」）、ElectricPoleのinterface化、プレビュー鮮度、パフォーマンス最適化、後方互換
  - 許容トレードオフ: 電柱判定を実行時コンポーネントからマスタ由来resolverへ一本化、共有コアをServer.Protocolに配置しクライアントが参照、コレクタ公開APIの不変維持
  - 制約: partial禁止・200行以下・try-catch禁止・デフォルト引数禁止・`Func<>`禁止・1ディレクトリ10ファイル・コメント日英2行セット・選定ルール（最寄り電柱1本→未接続機械を残容量まで、距離昇順→InstanceId昇順の全順序）・候補に自ブロックを含めない契約

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | file-too-long 1件（テスト270行・努力目標のみ）。比較演算子候補1件→verifierへ |
| 比較演算子verifier | 1 | `Selector.cs:35` の `>` を数直線順へ（機械的） |
| implicit-cardinality-assumption | 0 | 実行時状態の縮約でレンズ対象外。むしろ順序一元化で改善方向 |
| master-data-defense | 0 | optional新設なし・`?? Default`なし・interface3キーは必須＋default |
| precedent-alignment | 0 | 5つの新形すべてに役割同型の前例を実在確認。設計判断1件（判定源の一本化） |
| server-state-sync | 0 | 新規サーバー可変状態なし。Warning: 候補適格性のサーバー/クライアント非対称 |
| set-once-dependency-injection | 0 | setter注入ゼロ。readonly struct＋static純関数で本レンズの正解形 |
| type-driven-structure | 0 | Warning: switch case順序のload-bearing化・候補型が未解決IBlockParamを運ぶ・電柱判定の2系統化 |
| file-directory-organization | 0 | AutoConnect/6ファイル・行数内。Warning: UnitTest/Server/がテスト階層をミラーせず上限接近 |
| implicit-value-meaning | 0 | 判定源の置換3種すべて等価を実コードで裏取り。Warning: `ownUsedCount` の契約非対称・境界テストの上限ハードコード |
| test-mutation-effectiveness | 1 | 5ブロック種のinterface付与が剥離しても全テスト緑（resolverを通るテストがゼロ） |
| user-intent-fulfillment | 0 | 依頼動詞ペア6件すべて達成を`+`/`-`行で確認。Warning: 端点判定のガードがyaml規律のみに |
| architecture-lifecycle | 0 | 汎用レイヤ混入なし・Protocol閉じ込めなし。Warning: 電柱/機械分岐の2アダプタ複写 |
| bug-fix-intent | 0 | 症状surfaceを過不足なくtouch。Warning: switch順序契約の未明示・候補適格性の非対称 |
| caller-orchestration-minimization | 1 | `ElectricWireAutoConnectService` の容量0ガードがコアと完全二重化（削除で挙動等価） |
| centralization-duplication | 1 | 同上（独立に同一結論）。Warning: `positions`辞書が既存索引の再実装 |
| dead-code-and-scope | 1 | `BuildReceivedCandidates` が参照1箇所のprivate補助メソッド |
| region-internal | 1 | 同上（独立に同一結論）。ローカル関数＋`#region Internal`へ |
| result-state-propagation | 0 | 区別すべき結果の潰れなし。Warning: 電柱判別の型sniff後退・`CurrentConnectionCount`の二義 |
| schema-design | 0 | interface3キーは型・default とも8実装と完全一致。Warning: case順序依存・候補型のunion表現 |
| unidirectional-flow | 0 | 収集→判断→適用の3相分離を達成。Warning: 型テストによる判断源の二重化 |
| Fable全般 | 0 | 自ブロック除外を4経路すべて構造的に確認。Warning: 満杯判定源の2系統 |
| Codex外部監査 | 0 | Critical 0 / High 0 / Medium 2 / Low 7。リグレッションなしを7項目で個別裏取り |
| comment-rationale-guard | 1 | クライアントクラスドキュメントから「サーバーのワールド状態に触れない」禁止則が消失 |
| comment-convention-guard | - | 機械的17件＋要判断8件を適用、load-bearing 8件は残置（ガード自己裁定） |

## 適用した修正
コミット `be72f0617`:
- `Selector.cs:35` 比較演算子を数直線順へ（比較演算子verifier）
- `ElectricWireAutoConnectService` の容量0ガード重複を削除（caller-orchestration ＋ centralization の2系統一致。コアと条件式・分岐先が同値であることを実コードで確認）
- `BuildReceivedCandidates` をローカル関数＋`#region Internal`へ（dead-code-and-scope ＋ region-internal の2系統一致）
- resolverのswitch case順序に契約コメント追加（type-driven ＋ bug-fix-intent ＋ schema-design の3系統一致・案A採用）
- resolverのパターン変数名 `machine` → `wireConnectParam`（Codex ＋ タスクレビュー繰延Minor）
- `ToConnectorResults` の戻り値タプルに要素名を付与（Codex ＋ 繰延Minor）
- クライアントXMLドキュメントを日本語塊→英語塊へ並べ替え（Codex ＋ 繰延Minor ＋ precedent Info）
- コメント25件を規約長へ短縮（convention-guard裁定）

コミット `e76c5de08`（AskUserQuestion裁定の反映）:
- `SelectPlacementTargets` を新設し電柱/機械分岐をコア内1箇所へ集約。`SelectPoleTargets`/`SelectMachineTargets` をprivate化。サーバーは `CollectTargets` 1本へ統合し `ElectricWireAutoConnectService` の三項を除去、クライアントも三項を除去
- クライアント候補を `ElectricWireStateChangeProcessor` を持つブロックに限定
- `ElectricWireBlockParamResolverTest` を新設（接続数を宣言する全ブロックがresolverで解決できる不変条件。テストmodの18ブロック＝interface 8種すべて＋電柱を走査）

検証: コンパイル 0エラー0警告 / `ElectricWire|ElectricConnectionRange|WireContract` 95/95 PASS

## 設計判断（AskUserQuestion裁定）
- Q: 5ブロック種のinterface付与が剥離しても全テスト緑（test-mutation Critical） / 選択肢: データ駆動テスト・8種列挙TestCase・見送り / 裁定: **データ駆動テストを追加** / 適用: `ElectricWireBlockParamResolverTest`（`e76c5de08`）
- Q: 電柱/機械分岐が両アダプタに複写（5系統一致） / 選択肢: 現状維持・`SelectPlacementTargets`集約 / 裁定: **集約** / 適用: `e76c5de08`
- Q: 候補適格性のサーバー/クライアント非対称（4系統一致） / 選択肢: 現状維持・クライアントにも端点判定 / 裁定: **端点判定を追加** / 適用: `e76c5de08`（下記「事後結果」に限界を記載）
- Q: 削除された「サーバーのワールド状態に触れない」禁止則の復元（rationale-guard Critical） / 選択肢: 復元・しない / 裁定: **復元しない** / 適用: なし

## 破棄した指摘
- Codex「`ElectricWireAutoConnectService` の容量0ガード重複は対象差分外の既存コード」— 実コード照合の結果、旧実装は容量を `out _` で捨てており重複は本diffが新設したものと確認。2系統のCritical判定を採用
- test-mutation「単体テストの期待値がテストMod実値に暗黙依存」— 全テストの座標をマスタ実値と照合済みで現状一致。カバレッジ磨き上げのためMinor扱い
- 各系統が挙げた「満杯判定源の2系統」「クライアント経路のテスト不在」「候補配列の全ブロック規模確保」— contextで既知・繰延済みと明示した項目。重大度の再判定でも昇格せず

## 事後結果（マージ後追記可）
- **クライアント端点判定の限界（実装後に判明）**: `ElectricWireStateChangeProcessor` の付与条件（`BlockGameObjectPrefabContainer.cs:161-164` の `IsElectricWireConnectable`）は resolver そのものであるため、この絞り込みはサーバーの `IElectricWireConnector` コンポーネント確認と**等価ではない**。得られたのは候補列を電気系ブロックのみに縮めることと構造的対称性であり、レビュー4系統が名指しした「将来interfaceだけ付けてテンプレートを作らないブロックでプレビューに幽霊接続線が出る」リスクは**閉じていない**。真に閉じるにはサーバー側からの端点シグナルが要る
- UnitTest/Server/ が9ファイルになり10ファイル上限に到達間近。次の追加時に `UnitTest/Server/ElectricWire/` を切ってElectricWire系5件を移すのが妥当（file-directory-organization Warning）

## メタ
- セッションID: session_014nYFpVTYWgQp2WyVhtPcJM
- スキップ系統: なし（Codex実行あり）
- 備考: SDD（subagent-driven-development）の最終ブランチレビューとして実行。タスクごとのレビューで繰延された8項目をcontextに明示し、重複報告を避けたうえで重大度の再判定を許可した
