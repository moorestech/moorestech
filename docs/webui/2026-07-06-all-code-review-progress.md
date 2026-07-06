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

### 作業キュー（承認済み・実行中）
**第2波A: C#系fixエージェント** → **完了（2026-07-07 01:10頃）** 検証: compile ErrorCount 0 / テスト51 PASS（F3新規4件含む）
- F1: MoveItem検証を LocalPlayerInventoryController.TryMoveItem(out denyReason) へ集約、両ハンドラ縮小。エラーコード不変（commit `a4ad7302a`）
- F2: ItemRecipeViewerDataContainer.EvaluateVisibility へSSOT化。uGUIのDebug強制表示・topicの警告フォールバックは呼び出し側残置、表示結果不変（同上）
- F3: CraftExecuteActionHandler.ResolveCraftRecipe を純関数抽出、CraftActionTest.cs にNUnit 4ケース追加（同上）
- F4: **サーバー拡張不要と判明し実装完了**。流体量(FluidMachineInventoryStateDetail)・進捗(CommonMachineBlockStateDetail.ProcessingRate)は既存 va:event:changeBlockState で受信済み。BlockSubInventorySource に状態アクセサ追加、BlockInventoryTopic で FluidSlots/Progress 構築＋状態イベント購読で再配信。DTOは BlockInventoryDto.cs へ分割（commit `8ea8c9f18`）
- F5: AGENTS.md try-catch規約へ外部境界例外を明文化（commit `280696b8a`）
- F6: WebUiModalService.CancelPending 追加、ClearBindings から呼び出し。TODO.md 追記（同上）
- レポート: `.superpowers/sdd/acr-fix2-cs-report.md`

**第2波B: Web系fixエージェント** → **完了（2026-07-07 00:xx）**
- bridge/ 15ファイル → transport(7)/store(4)/contract(3) へ分割、index.tsバレル維持、deep import・vi.mock・fixtureパス追随（commit `b641527f9`）
- features/recipe/ 11ファイル → 末端ビュー6つを views/ へ移動（commit `b2cbb2613`）、e2e mock-host のimport追随（commit `52ee782f1`）
- 検証: tsc EXIT 0 / vitest 80 passed / e2e 24 passed — ベースラインと同一値で挙動不変確認
- レポート: `.superpowers/sdd/acr-fix2-web-report.md`

**第3波: 200行超ファイル分割** → **完了（2026-07-07 02:00頃）** compile 0 / テスト51 PASS・partial不使用・公開シグネチャ不変
- e2e mock-host server.ts(333) → 責務別6モジュール（最大 wsHandler 140行）（commit `b561483a4`）
- PlayerInventoryViewController 423→112: Main/Interaction/ へ操作解釈3クラス抽出（commit `0719dd09e`）
- InitializeScenePipeline 382→182: Initialization/ へ初期化フェーズ3クラス抽出（commit `18e041036`）
- ViteProcess 256→172 / LocalPlayerInventoryController 226→192 / HotBarView 224→157（commit `af6d00acf`）
- 留意: InitializeScenePipeline はブート経路のため実挙動の最終確認はPlayModeスモークが望ましい（レポート記載）

**feature/webui-block-research-ui 取り込み** → **完了（2026-07-07 02:20頃）**（ユーザー承認済み5ステップ計画）
- マージ競合なし（88ファイル +2651/-125）。予告どおりF4版BlockInventoryDto.csがfeature版BlockDetail/BlockInventoryDtos.cs（スーパーセット）と型重複→F4版を削除しfeature版に一本化（commit `ce28b9af0`）
- 検証: uloop compile 0 / C#テスト57 PASS（新規BlockDetail/Research系6件含む）/ tsc 0 / vitest 122 / e2e 34 — 期待値と完全一致
- マージ後整理: Research系3トピックを Topics/Research/ へ移動、F4残置の未使用アクセサ3件削除（commit `9e21b2748`）。BlockName等プロパティ3件はfeatureコードが使用中のため残置
- 決定論再チェック結果: web側200行超ゼロ。10ファイル規約違反3件（WebUiHost/Game/Actions 11・blockInventory/views 11・e2e/tests 11）→ 解消作業中
- レビュー範囲外の既存200行超（記録のみ・未対応）: MainGameStarter 327 / ResearchTreeElement 279 / CommonSlotView 235 / CraftInventoryView 213（いずれもブランチdiff外）

**クローズ処理**
1. 最終QA: uloop compile / NUnit / tsc / vitest / e2e 全green + 決定論チェック再実行（dir-file-limit・file-too-longの解消確認）
2. 本ドキュメントとレジャー更新、AGENTS.mdタスクリストから「all-code-review」行を削除
3. /tmp の一時ファイル削除

### その後のフェーズ
- 「ステート、プロップス、Context、状態管理ライブラリの利用の適正化」— レビュー全系統でCritical 0のため軽量確認で足りる見込み
- 「実装漏れの徹底洗い出し」— 種リスト: Shift直接移動のSubInventory非対応 / BlockItemGrid右クリック系 / blockInventory e2e拡充 / ui_state.requestホワイトリスト / useItemMaster staleキャッシュ / モーダルプロデューサ配線 / crafting validators深掘り（今回見送り分）/ 既存記録分（block右クリ・Esc close・13種ブロックビュー・列車インベントリ）/ **研究報酬アイテムの個数表示**（feature/webui-block-research-uiの保留タスクから移設）

## 未コミットの残置ファイル（ツール副産物・要ユーザー判断）
- `.moorestech-external-revisions.json`（Task4検証時の旧pin。恒久対応要判断）
- `moorestech_client/.uloop/tools.json` / `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（uloop副産物）
