# イベント出展向け 序盤進行不能バグ監査（開幕〜鉄の時代）

- 実施日: 2026-08-26
- 対象コード: origin/master c017bff7（比較元 fix/skit-ground-position-3x3 2ec8e5bc8）
- 対象マスタデータ: moorestech_master 200ab3c9（origin/master のピン）
- 方法: コード読み＋データ到達性シミュレーション（Unity実行なし）。7観点（サーバー側チャレンジ連鎖 / クライアントチュートリアルUI / 開幕スキット・起動パイプライン / 序盤ゲームループ / ビルド・データ境界 / 研究ツリー到達性 / 研究・機械・動力システム）を独立に調査し、指摘は実コード・実データで照合した
- 前提: 自動生成マップ・既定 seed 196・草原スポーン・イベントモード（`MOORESTECH_EVENT_MODE=1`）

## 結論

- マスタデータ由来のデッドエンドは **無し**（チャレンジ32本・原始研究1〜9・6分岐・鉄の時代まで全コストに解放済み入手経路あり）
- ビルドを切る前に潰すべき起動不能級が **4件**（A）
- 出展中に「固まった」になり得るコード起因が **6件**（B）
- ロックではないが「壊れている」と見える序盤の詰まりが **5件**（C）

## A. ビルドを切る前に必ず潰すもの（起動不能級）

| # | 内容 | 根拠 |
|---|---|---|
| A1 | origin/master のワールド既定がまだ Template（`moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs:18`）。Template は地形が無い（`WorldProvisioner.cs:148-152`、bd vq12）。生成マップ既定（fe81ecd37 / ADR 0035）は PR #1271 `feature/new-world-defaults-to-generated` にしか無い。**出展ビルドは #1271 ブランチ（またはマージ後の master）から切ること** | 手動確認 |
| A2 | Localization SourceGenerator マーカーが古い。origin/master の `Client.Localization/_CompileRequester.cs` = `E7-5F-…`、`Localization/` 実内容からの期待値 `D3-F3-1A-5A-…`。過去に同原因で CS0117×26 の Release ビルド失敗（PR #1269 で修正、#1268 マージで再ズレ）。**クリーン Library でビルドする前に force-recompile でマーカー再生成** | `SchemaWatchOrchestrator.ComputeRequesterToken` を再実装して算出 |
| A3 | `Client.Game/InGame/Map/MapObject/Instantiation/MapObjectInstantiationRunner.cs:64-77` は近傍インスタンス化が1件でも失敗すると起動を例外で落とす。release-20260826c の build.log:19164 で `StratMesaSharp_0.prefab` が BrokenPrefabAsset として strip されていた。メサが近傍圏に入る seed だと起動死。**ビルドログの strip 警告をゼロにする** | build.log / player-1.log |
| A4 | プレイテスト DSL / StandaloneQa を出展機で走らせた履歴があると、CWD 相対 `../cache/BoolDebugParameters.json` に `SkitPlaySettings=true` / `FreeBlockPlacement=true` が残り、開幕スキットスキップ・全ブロック解放になる（`Common.Debug/DebugParametersCacheDirectory.cs:20,68`）。**出展機の `cache/` を削除** | 手動確認 |

補足: 開幕スキットの座標は seed 196 固定前提のハードコード（`AddressableResources/Skit/skits/100_start_game.json`、スポーン相対＋地上パート +1.86m）。seed や generation.json を変えたら再確認が要る。進行は阻害しない（キャラ・カメラに当たり判定無し）。

## B. 出展中に「固まった」になり得るもの（コード起因）

1. **開幕スキットは Web UI が唯一の進行手段**。`WebUiScreenGate.cs:19` `IsWebUiMode => true` 固定、`SkitManager.cs:152` で uGUI スキット UI は常時 off、`Client.Skit/UI/SkitIntentWaitController.WaitForAdvanceAsync` はキーマウのフォールバック無し。CEF/WebUiHost（ポート 25050–25069）が立たない・React ツリーが死ぬ（`main.tsx:27` 単一 ErrorBoundary）と、スキップ不能・Esc も効かず（`SkitState`）、アイドル終了→再起動→同じ失敗のループ。出展機で WebUI 起動を目視確認する以外の保険が無い。
2. **キー/ボタン押しっぱなしでアイドル終了が永久に発火しない**（bd moorestech-iopd）。`Client.Starter/EventMode/EventIdleQuitWatcher.cs:59,63` が `isPressed` を活動扱い。これが唯一の自己復旧経路（`EventModeAutoStart` は `RuntimeInitializeOnLoadMethod` の1回限り）。修正は `wasPressedThisFrame || wasReleasedThisFrame` に限定するだけで足りる。
3. **起動時間がアイドルタイムアウト(既定180s)に算入**。`EventModeAutoStart.cs:33` で watcher 生成→`GameInitializedEvent` まで `_idleSeconds` 未リセット。現状 ~30s だが初回シェーダコンパイル等で伸びると起動中に終了→無限再起動。
4. **ポーズメニュー「セーブして終了」が確認なしでプロセス終了**（`features/pauseMenu/PauseMenuPanel.tsx:17` → `SaveAndQuitPresenter.cs:34` → `QuitApplicationAsync`）。Esc→2つ目のボタンで来場者が落とせる。
5. スキット `100_start_game.json` id 31「パチン！」が全画面暗転（id 128〜129）中にクリック待ち。`.transition` は `pointer-events: none` なので見えない窓領域クリックで進むが、初見には固まって見える。
6. `UIStateControl.Initialize(GameScreen)`（`MainGameInitializationFinalizer.cs:92`）がスキット再生中に1フレーム InGame へ再突入し、その1フレームで Esc/Tab/数字キーが別 State へ逸れる。ロックではない。

## C. 序盤の詰まり（ロックではないが「壊れている」と見える）

1. **最初のブロック（風力掘削機・石窯＝Electric 系）は設置不可のとき無反応**。`PlaceSystem/Common/CommonBlockPlaceSystem.cs:208` の `!wirePlaceable` で送信前 return（`ElectricWireAutoConnectPreview.cs:66,123` はコスト不足・重なりも false を返す）。音も通知も出ない。非電気ブロックはサーバー拒否通知が出る。
2. **ホイールで素手(-1)に当たる**。`features/inventory/EquipmentPanel/equipmentLogic.ts:10-16` が `slotCount+1` で周回。スロットが何も光らず（`index.tsx:100-107`）、チュートリアルアンカーも消え、装備チャレンジ（`EquipItemChallengeTask.cs:89-93`）も伐採（`MapObjectMiningService.cs:62` NoTool）も進まない。さらに `LocalPlayerEquipment.cs:65-67` がインベントリ開閉で `SelectionConfirmationRevision` を上げ、保留中のホイール要求を落とす。
3. **粉砕機が燃料式風車1基で永久停電**。粉砕機 `baseRpm 5 / baseTorque 36 / torqueExponentOver 1.585`、風車 `10rpm / 90torque`。10rpm で要求トルク 36×2^1.585≈108 > 90 → `GearNetworkPowerCalculator.cs:44-47` が停電、稼働率0で燃料も減らず需要も下がらない。砕いた石材・青銅鉱石の粉は手クラフト可なので鉄の時代には到達できる。
4. 設置 Y が `floor(hitY)`（`PlaceSystemUtil.cs:114`）で地形に埋まる。`GroundCollisionDetector` は Rigidbody 無しで `OnTriggerStay` が発火しない。
5. 研究コストのグラインド: 原始研究7〜木材の組み立てで板400/棒600/木釘600級、鉄の時代は手持ち45スロット中24スロット分（約2500個）を同時保持（`ResearchDataStore` はチェストを数えない）。

## D. 問題なしと確認した範囲（masterピン 200ab3c9）

- チャレンジ32本: 全 guid 解決・単線・各クラフト系チャレンジの前に対応研究が挿入済み（旧ピン 60e815a にあった「ロック中アイテムを要求」3件は解消）
- 研究: 原始研究1〜9・6分岐・鉄の時代まで全コストに解放済み入手経路あり。prereq は ALL 判定だが全到達可。重複 `researchNodeGuid` 無し（`019e3af0…` 2件は別 guid）
- 石窯/風力掘削機は `requiredPower 0` で無配線稼働。原木/石/粘土/青銅は 250m 圏に生成、石器/石の斧で手掘り可
- チャレンジ完了検知: 初期データ/イベントのレース無し、ハイライト残留は `pointer-events: none` でクリック非阻害、MapObject ピンは k-d tree 化済み
- 起動パイプライン: BoundPort/接続 60s タイムアウト、`InitialEventApplyWaiter` の3ターゲットは例外を完了へ畳む。スキット終了は `finally Cleanup()` で一括復元
- 未対応 OS ロケールは english へフォールバック、イベントモード誤設定はサイレント無効、セーブ先は書込可

## 推奨アクション（優先順）

1. 出展ビルドは PR #1271 ブランチ（または master へマージ後）から切る
2. ビルド前に Localization マーカー再生成（A2）、BrokenPrefab strip 警告ゼロ（A3）、出展機の `cache/` 削除（A4）
3. コード修正で安い順: B2（iopd）→ B4（終了確認 or イベントモードで非表示）→ B3（GameInitialized までタイマー停止）→ C1（無反応の通知）→ C2（-1 除外）
4. データ修正: C3（粉砕機の baseTorque を下げるか風車2基前提を明示）、B5（id 31 を暗転解除後へ）

## 参考

- 研究到達性シミュレーション: 開始時解放状態から研究を prereq 順に辿り、各 consumeItems が「手入手 / 解放済みクラフト / 解放済み機械レシピ（機械ブロック解放＋建設可）」のいずれかで生産可能かを再帰判定。同アルゴリズムを再実行する場合は `ItemRecipeViewerDataContainer.EvaluateVisibility` の可視性ゲートを模す
- 関連 bd: moorestech-iopd, moorestech-vq12, moorestech-tlza, moorestech-muyo, moorestech-roaw, moorestech-n7r（PR #1174 で修正済み・status stale）
