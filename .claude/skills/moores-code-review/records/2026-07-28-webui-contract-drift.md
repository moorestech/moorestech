# webui-contract-drift レビュー記録 (2026-07-28)

## 対象
- base: `e202cf6ce` / reviewed head: `467f2345b`（適用修正後の最終: `679a702eb`）
- ブランチ: tree3 / PR: 未作成
- context要約 — ゴール: Web UI⇔Unity契約drift 6点の修正（typed registry・action名パリティ・block-icons・input_state明示受理・死んだvariant削除・偽topic名修正） / 非目標: defineTopic一本化・cross-language codegen・mock入力排他 [ユーザー裁定 2026-07-28] / 許容トレードオフ: GetUninitializedObject・WireFixtures相対パス跨ぎ・最小形式meta [agent前提] / 制約: WireFixtures前例・mockはsrc契約をimport

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 1 | dir-file-limit(Client.Tests/WebUi 20本)→ユーザー裁定でテスト適用外化 |
| domain-boundary | なし | playtest契約漏れWarning・input_state直結/フラットDTOは既存違反 |
| precedent-alignment | 1 | @/bridge深層import(唯一のbarrel違反)→importOriginal化 |
| type-driven-structure | なし | 重複ActionType/非定数ガード2本をWarning→適用 |
| file-directory-organization | なし | dir超過はWarning留め(既存9本移動が過検知ガード抵触) |
| implicit-value-meaning | 1(降格) | mock event経路の{modal:null}→stripNulls実在で反証・コメント修正のみ |
| test-mutation-effectiveness | なし | 6ガード中CI強制はC#1本のみ→CIジョブ追加裁定 |
| user-intent-fulfillment | なし | 6ゴール全達成を実測確認 |
| cs/ts-centralization, single-source | なし | アイコンprefix並行実装Warning→定数共有化 |
| ts-ai-recurring-mistakes | 1 | 深層import(precedentと同一)・hasOwnガード提案 |
| bug-fix/dead-code/region/caller/schema/unidirectional/result-state | なし | 削除3件の死亡を独立grepで裏付け・helper region化2系統 |
| Codex外部監査 | なし | Medium1(dir超過)・Low2(重複吸収・prototypeキー) |
| Fable全般(opus代替) | なし | server closeリークWarning→適用・playtest案C提示 |

fable指定2体(precedent-alignment/全般俯瞰)はガードフックのモデルポリシーによりopusで代替実行。

## 適用した修正
- slotActions.test.tsをimportOriginal形へ（2系統一致） → `068a90d0b`
- WireContractActionNamesTest: helperローカル関数+#region Internal化（2系統）・重複/非定数assert（3系統） → `068a90d0b`
- topicFixtures: Object.hasOwnガード（Codex）・modalコメント機構修正（2系統） → `068a90d0b`
- アイコンprefix定数をhttpEndpointsからexportしmockが参照（2系統） → `068a90d0b`
- SendSnapshotAsync private化（2系統） → `068a90d0b`
- httpHandler.test.tsのonTestFinished close（俯瞰） → `068a90d0b`
- topic-conventions.md 型検査保証の限定注記（3系統） → `068a90d0b`

## 設計判断（AskUserQuestion裁定）
- Q: playtest.dom_query_resultの契約上の扱い（6系統一致） / 裁定: 除外を明示+走査拡大 / 適用: Client.*全アセンブリ走査+excludedFromWebContractデータ宣言+TS側非混入検査 `679a702eb`
- Q: dir-file-limit(20本)のサブディレクトリ移動 / 裁定: **テストは200行/10ファイル規約の適用外**（新規恒久ルール） / 適用: checks_static.pyに_is_test_path除外+SKILL.md明記 `2dc83aa85`
- Q: wireFixtures.tsの配置 / 裁定: test-helperリネーム / 適用: wireFixtures.test-helper.ts `2dc83aa85`
- Q: CIゲート / 裁定: webuiジョブ追加 / 適用: run_test.ymlにvitest+本体/e2e型検査 `2dc83aa85`

## 破棄した指摘
- mock event経路の{modal:null}がschema違反形を送る(implicit-value Critical) — wire.tsのstripNullsが送信直前にnullキー除去、ワイヤ同形を4系統+Codexが確認
- コメント長候補9件の短縮(convention-guard) — 全件が相互参照/根拠コメントで短縮案は固有情報を喪失、残置裁定
- .meta手動作成疑い — 既存WireContract系と同一の最小形式・インポート後不変を確認

## 未対応の残課題（フォローアップ候補）
- RegisterAction登録漏れ検出（クラス実装⇔hub登録の乖離、要PlayMode統合テスト・3系統）
- mock event push約20箇所/topicOverrides/state.tsのas型検査外（sendTopic案・result-state）
- DEMO分岐（blockアイコンplaceholder）テスト無カバレッジ・block/item同一絵（2系統）
- domQueryResponder.tsの生文字列sendAction（web側playtest名は依然無検査）
- allScreensI18n.test.tsの偽topic名mock残存（既存）
- WsClientMessageフラットDTO・input_stateのdispatcher直結（既存負債）

## 事後結果（マージ後追記可）
-

## メタ
- セッションID: session_014ft6zEiKGr4vQpXf2M6a4E / スキップ系統: なし / 備考: Codexのread-onlyサンドボックス内uloop失敗が.moorestech-external-revisions.jsonを汚染（restore済み）
