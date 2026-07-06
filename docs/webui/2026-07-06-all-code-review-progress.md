# WebUI 全体レビュー進捗と残タスク（2026-07-06 23:15 時点）

## ここまでの進捗

### 1. uGUIパススルー計画（2026-07-06-webui-ugui-state-passthrough.md）完了
- Task 1〜5 完了。未実施だった Task 5 のレビューを実施し **Approved**
- レビュー副産物:
  - 未コミットだった `UIStateControl.cs` の変更（Initialize内での `_lastWebUiMode` 同期）は **no-op と判定しリバート**（Initialize は sceneLoaded 時＝WebUiCefToggle.Start より前に走るためゲート未書込）
  - 代わりに本物の潜在バグを修正: 乗車中ログイン時（Initialize(TrainHUDScreen)）に起動時偽エッジが TrainHUD を GameScreen へ蹴り出す問題。`_lastWebUiMode` を `bool?` 化し初回 Update でシードする方式で修正（commit `e381dba28`、compile 0 / WireContractTest 9/9、再レビューApproved）
  - 留意invariant: WebUiCefToggle が UIStateControl の初回 Update と同一シーンロードフレームで Start する前提。CEFブートストラップを遅延活性化に変える場合は再考

### 2. AGENTS.md タスクリスト消化状況
- ~~tailwindを全面的にやめてmantineに移行する~~ → **完了済みと確認**（commit `4810add12` で完全撤去、残骸ユーティリティクラスもゼロ）。リストから削除済み（commit `3975c4077`）
- **all-code-reviewを使った全体観点でレビュー → 実施中（本ドキュメントの主題）**
- ステート/プロップス/Context/状態管理ライブラリの適正化 → 未着手（※下記レビューで関連観点は概ねクリーン判定）
- 実装漏れ（uGUIにあってwebにないもの）の徹底洗い出し → 未着手（レビューで判明した分は下記に記載）

### 3. all-code-review 実行状況（対象: web-uiブランチ全体 bad8405018..HEAD、TS 10,718行diff + WebUI関連C#）
回収完了: 決定論チェック + v3 reviewer 42体 + Codex外部監査 + Fable全般レビュー = **全44系統**

#### Critical/High（裏取り・重複排除後）
| # | 指摘 | 出所 | 対応 |
|---|---|---|---|
| 1 | **WebUiHost起動失敗（ポート5050衝突・node欠如）でもwebモードゲートONのまま→uGUIインベントリ非表示+Web死亡=無UI・回復不能**。ゲートを「CEF ON かつ ホスト起動成功」のANDへ。Vite起動失敗も失敗扱いに統合 | Fable[検証済み]+Codex | **fix適用中(M6)** |
| 2 | debug.echo(EchoActionHandler)が本番ビルドでも常時有効 | dev-prod | fix適用中(M5) |
| 3 | fillRatioがclamp01を再実装 | 2系統一致 | fix適用中(M1) |
| 4 | #region Internal規約違反（高確信9箇所: HotBarView/UIStateControl/Topic4種/InventoryTopic.ToDto/BlockInventoryActions/WireContractTestクラス直下region） | region-internal+dead-code+codex | fix適用中(M3,M4) |
| 5 | XMLコメントJP/EN交互違反2件（WebUiScreenGate/WireContractTest） | unity-conv | fix適用中(M2) |
| 6 | 新設3イベントがC#標準event（規約はUniRx）: ProgressBarView/SubInventoryState/WebUiModalService | Fable | fix適用中(M7) |
| 7 | MoveItem検証ロジックがInventoryActions/BlockInventoryActionsで逐語重複（コントローラ内部ガードの写し）。LocalPlayerInventoryController.TryMoveItem(out denyReason)へ集約案 | central-dup+caller-orch 2系統 | **設計判断（ユーザー確認へ）** |
| 8 | RecipeViewer表示フィルタ二重実装: ItemListView.IsShow × RecipeViewerItemListTopic.IsShow（乖離をコメントで自認） | cs-ssot | 設計判断 |
| 9 | WebUiModalService.RequestModal=呼び出し元ゼロ（モーダル機能はプロデューサ未配線）+ static pending未破棄 | dead-code+codex | 設計判断 |
| 10 | BlockInventoryTopicがFluidSlots空/Progress=null固定→実ホストから流体・進捗が届かない（fixture/Web UIは対応済みで乖離） | Codex High | 設計判断 |
| 11 | try-catch 13箇所（規約: 基本禁止。全て外部境界隔離との評価も複数系統） | 決定論+Codex | 設計判断（規約例外の明文化 or 書換） |
| 12 | 200行超5ファイル（PlayerInventoryViewController 424 / InitializeScenePipeline 378 / mock-host server.ts 333 / HotBarView 221 / LocalPlayerInventoryController 205） | 決定論+Codex | 設計判断（分割方針） |
| 13 | bridge/直下15ファイル（規約10）→ transport/store/contract分割案。features/recipe/11ファイル→views/分割案 | 決定論+file-dir-org | 設計判断 |
| 14 | CraftExecuteActionHandlerのunlockゲート（invalid_recipe/recipe_locked）がテスト0=mutation耐性なし。純関数抽出+NUnit3ケース案 | test-mutation | 設計判断（テスト追加） |
| 15 | crafting系validatorが浅い（recipes配列の存在のみ）→壊れpayloadでReactクラッシュ可能 | Codex Medium | 設計判断（深掘り実装） |

#### 実装漏れフェーズ（AGENTS.mdリスト4番）へ送る指摘
- Web directMove（Shiftクリック）が main↔hotbar 限定。uGUIはSubInventory中に main/hotbar↔block 直接移動あり（Codex High）
- BlockItemGridが左クリック丸ごと移動のみ。uGUIの右クリック半分取得/1個置き/ドラッグ系が未対応（Codex Medium）
- blockInventory e2e が左クリックpickupのみでパリティ欠落を検出できない（Codex Medium）
- 既存記録分: block右クリ/Shift/ダブルクリック、Escでのblock UI close、13種ブロックビュー、列車インベントリ
- useItemMasterのモジュールキャッシュがWS再接続後もstale（外部ブラウザ開発フロー限定の実害、Fable Warning）
- ui_state.request が現stateを問わず受理される（Story/PauseMenu中の遅延要求で強制遷移し得る。ホワイトリスト検討、Fable Warning）

#### クリーン判定だった主な観点
状態管理（zustand SSOT・一方向フロー・React antipattern・schema設計・結果状態伝播・構築所有権・メンバ配置）はC#/TSとも全系統Criticalなし。「ステート適正化」タスクの事前調査としては良好。

## つぎやること（順番・2026-07-06 23:40更新）

### 完了済み（追記）
- 機械的修正 M1〜M7 適用済み・全テストgreen（commits `92a0fc34c` `e6815edc9` `bd03e70b2`）
- post-check: rationale-guard Critical 0 / convention-guard 機械的6件適用（commit `e74f10fa4`）、要判断2件は根拠保全優先で温存
- 設計判断をユーザー確認済み。回答: 品質修正3件適用（validators深掘りは見送り）/ モーダルは記録のみ+pending破棄 / 流体・進捗は実配信を実装 / try-catch明文化+bridge・recipe分割+200行超分割すべて実施

### 作業キュー（承認済み・これから実行）
**第2波A: C#系fixエージェント**（仕様: scratchpad/acr-fix2-cs-spec.md）
- F1: MoveItem検証の集約 — LocalPlayerInventoryController.TryMoveItem(out denyReason) へ1本化、両ハンドラはパース+マップのみに縮小
- F2: レシピ表示フィルタ一本化 — ItemListView.IsShow × RecipeViewerItemListTopic.IsShow を単一評価器へ（uGUIのデバッグ強制表示とtopicの警告フォールバックは呼び出し側に残す）
- F3: craft unlockゲートのテスト — 判定を静的純関数抽出しNUnit 3ケース以上
- F4: BlockInventoryTopic 流体/進捗の実配信 — uGUIのデータ源を調査して配線。サーバープロトコル拡張が必要ならBLOCKED報告で止める
- F5: AGENTS.md try-catch規約に「外部境界の隔離のみ許容+根拠コメント必須」を明文化
- F6: WebSocketHub.ClearBindings で modal pending を破棄 + TODO.mdに「RequestModalプロデューサ未配線」を記録

**第2波B: Web系fixエージェント** → **完了（2026-07-07 00:xx）**
- bridge/ 15ファイル → transport(7)/store(4)/contract(3) へ分割、index.tsバレル維持、deep import・vi.mock・fixtureパス追随（commit `b641527f9`）
- features/recipe/ 11ファイル → 末端ビュー6つを views/ へ移動（commit `b2cbb2613`）、e2e mock-host のimport追随（commit `52ee782f1`）
- 検証: tsc EXIT 0 / vitest 80 passed / e2e 24 passed — ベースラインと同一値で挙動不変確認
- レポート: `.superpowers/sdd/acr-fix2-web-report.md`

**第3波: 200行超ファイル分割**（C#分はF1完了後。LocalPlayerInventoryControllerがF1と重なるため順序必須）
- ~~e2e mock-host server.ts(333)~~ → **完了**: 責務別6モジュール（server 13/wire 24/state 28/inventoryModel 76/httpHandler 83/wsHandler 140行）へ分割、tsc/vitest 80/e2e 24 全green（commit `b561483a4`）
- 残: PlayerInventoryViewController(424) / InitializeScenePipeline(382) / ViteProcess(256) / HotBarView(225) / LocalPlayerInventoryController(205+F1増分)
- partial禁止のもと責務単位で通常クラスへ分割

**クローズ処理**
1. 最終QA: uloop compile / NUnit / tsc / vitest / e2e 全green + 決定論チェック再実行（dir-file-limit・file-too-longの解消確認）
2. 本ドキュメントとレジャー更新、AGENTS.mdタスクリストから「all-code-review」行を削除
3. /tmp の一時ファイル削除

### その後のフェーズ
- 「ステート、プロップス、Context、状態管理ライブラリの利用の適正化」— レビュー全系統でCritical 0のため軽量確認で足りる見込み
- 「実装漏れの徹底洗い出し」— 種リスト: Shift直接移動のSubInventory非対応 / BlockItemGrid右クリック系 / blockInventory e2e拡充 / ui_state.requestホワイトリスト / useItemMaster staleキャッシュ / モーダルプロデューサ配線 / crafting validators深掘り（今回見送り分）/ 既存記録分（block右クリ・Esc close・13種ブロックビュー・列車インベントリ）

## 未コミットの残置ファイル（ツール副産物・要ユーザー判断）
- `.moorestech-external-revisions.json`（Task4検証時の旧pin。恒久対応要判断）
- `moorestech_client/.uloop/tools.json` / `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（uloop副産物）
