# UITK 移行調査: C# ホストとデータの流れ

調査日: 2026-10-22／対象: moorestech リポジトリ（master）／読むだけで変更なし。

パスの略記:
- `Scripts/` = `moorestech_client/Assets/Scripts/`
- `Host/` = `Scripts/Client.WebUiHost/`
- `webui/` = `moorestech_web/webui/src/`

前提として読んだもの: `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、`docs/webui/ui-toolkit-migration-discussion-2026-10-18.md`、`docs/webui/topic-conventions.md`、`docs/adr/0052-ugui-removal-scope-and-exceptions.md`、`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`、ADR 0013/0016/0017/0019/0021、`docs/webui/design/input-focus-exclusivity.md`、UITK 難所 PoC レポート（2026-10-22）。

---

## 0. 結論

1. **状態の正本はほぼすべて Web の外（`Client.Game`・`Client.Skit`・`Client.Input`・`Client.Localization`・サーバー）にある。** `Client.WebUiHost` の Topic は「正本の変化通知を購読 → DTO を組む → JSON で配る」アダプタ。28 の topic のうち 18 前後は、正本をそのまま使い、配信を C# の購読に置き換えるだけで済む（§4 の区分 A/A'）。
2. **例外は `Client.WebUiHost` の中にある状態と取得処理。** 研究ノード状態、チャレンジ全量状態、電力網・歯車網の 1 秒ポーリング、開いたブロックのイベント購読、時刻表の取得状態機械、通知の連番と移行告知、モーダル要求キュー、出展モードの言語ゲートがこれに当たる（§6.2）。いずれも `WebUiGameBinder.Bind()` の中でしか生成されず、Hub が null のとき（Web ホストの起動失敗時）は丸ごと生成されない（`Host/Game/WebUiGameBinder.cs:48-55`）。UITK から使う前に、Hub と無関係に DI へ登録できる場所へ移す必要がある。
3. **Web にしか無いロジックは約 4,000 行の非テスト `.ts`（§6.3）にまとまっている。** 内訳は、名前付き補間を含む文言解決、通知の集約と文言表、画面ルーティング表、スロット操作の計画（左・右・ダブル・Shift 配分）、各詳細パネルの導出と整形、チュートリアル anchor の解決。大半は純関数なので、vitest を仕様として C# へほぼ 1 対 1 で移植できる。
4. **入力の取り合いは CEF 側では止められない。** cef-unity は EarlyUpdate で旧 `Input` を毎フレーム無条件に CEF へ送る。公開 API は `LoadUrl` だけで、入力を止める口が無い（`jp.juha.cefunity/Runtime/CefUnityBrowserSample.cs:553-590`）。共存中の裁きは C# 側の 2 か所に集める（`UiPointerHitTest` と `WebUiInputExclusivity`）。そのうえで「同じ画面位置に、両系の操作可能な要素を同時に置かない」を規則にする（§8）。
5. **同じ topic を両系が同時に購読することはできる。** 正本は UniRx で多購読に対応している。ただし、要求を 1 件だけ持つ単一消費者がある（モーダル・BP 名入力ブリッジ・スキットの入力）。画面をまたぐ横断レイヤー（掴み絵・ツールチップ・通知・トースト・チュートリアル枠）もある。これらは描画する系を 1 つに決める必要がある（§8.1〜8.2）。
6. **撤去は依存の順に進める。** 先に済ませるものは 3 つ。RemoteExec の HTTP 待受を Kestrel から外すこと（`Scripts/Client.RemoteExec/RemoteExecEndpoint.cs` は Kestrel の `HttpContext` 上で動く）、§6.2 の状態の移設、プレイテスト DSL の DOM 依存の置き換え（シナリオ 29 本）。この 3 つが済むまで `Client.WebUiHost` は消せない（§7）。

---

## 1. 全体の流れ（図）

```
┌────────────────────────── Unity プロセス ──────────────────────────┐      ┌──── cef-unity-server（Chromium・別プロセス）────┐
│ 正本: Client.Game / Client.Skit / Client.Input / Client.Localization  │      │ React 18 + Mantine 8 + zustand（moorestech_web）  │
│  LocalPlayerInventoryController, UIStateControl, SubInventoryState,   │      │                                                   │
│  ClientHotbarDatastore, PlaceSystemStateController, *StateStore ...   │      │                                                   │
│        │ UniRx Subject / ReactiveProperty / C# event（変化通知）       │      │                                                   │
│        ▼                                                              │      │                                                   │
│ Topic アダプタ  Host/Game/Topics/*Topic.cs                            │      │                                                   │
│  ・購読 → PostLateUpdate でフレーム末に1回へ畳む（INFRA-7）            │      │                                                   │
│  ・連続値は固定周期でサンプル（ui.progress 100ms / 歯車 IntervalFrame(4)）│     │                                                   │
│  ・BuildJson(): DTO → WebUiJson（camelCase・null は省略）              │      │                                                   │
│        │ hub.Publish(topic, json)                                     │      │                                                   │
│        ▼                                                              │      │                                                   │
│ WebSocketHub（Host/Boot/WebSocketHub.cs）                             │      │                                                   │
│  revision++ → {op:"event", topic, revision, data}                     │  WS  │ webSocketClient.ts onmessage                      │
│  購読中の接続ごとの送信 Channel ─────────── /ws（Kestrel 127.0.0.1:25050〜25069）──▶ deliverTopicPayload（zod 検証と変換）          │
│  subscribe 受信: 購読登録 → メインスレッドで GetSnapshotJsonAsync → snapshot │   │ → topicStore（revision 照合・restoring 状態）      │
│                                                                       │      │ → useTopic / useTopicSelector / readTopic         │
│ WebSocketMessageDispatcher（Host/Boot/WebSocketMessageDispatcher.cs） │ ◀─── │ dispatchAction（bridge/transport/actions.ts）      │
│  op=action → 接続ごとの action キューで到着順に1件ずつ                 │      │   {op:"action", type, requestId, payload}         │
│  → SwitchToMainThread → IActionHandler.ExecuteAsync(JObject)          │      │ subscriptionManager（参照カウント 0→1 で subscribe）│
│  → {op:"result", requestId, ok, error?}（受理の通知だけ。画面の更新は   │ ───▶ │ 失敗時は BENIGN_ERRORS 以外をトースト             │
│     必ず後続の topic で行う）                                          │      │                                                   │
│  op=input_state → Client.Input.WebUiInputExclusivity                  │ ◀─── │ useWebInputExclusivity（DOM のヒット判定とフォーカス）│
│  op=ping → pong（Web 側は 5 秒ごとに送り、15 秒無応答で切断）          │ ◀──▶ │                                                   │
│                                                                       │      │                                                   │
│ HTTP GET（Host/Boot/WebUiEndpoints.cs）                               │ ◀─── │ itemMasterStore / fluidMasterStore / i18n /         │
│  /api/master/items, /api/master/fluids, /api/i18n/{locale}?revision,  │      │ 言語一覧 / <img src=/api/...icons/..>               │
│  /api/i18n-languages, /api/{icons,block-icons,fluid-icons,             │      │                                                   │
│  train-car-icons,connect-tool-icons,blueprint-icons}/{key}.png,        │      │                                                   │
│  /api/assets/*, /api/remote-exec, /api/ping, 本番は dist の静的配信     │      │                                                   │
│                                                                       │      │                                                   │
│ 描画: MainGameUI.prefab の uGUI Canvas 上の RawImage に、GPU 共有テクスチャを貼る（CefUnityBrowserSample）◀── OSR ──┘
│ 入力: CefUnityBrowserSample が EarlyUpdate の PlayerLoop hook で Input.mousePosition / GetKey / inputString / IME を毎フレーム CEF へ送る
└───────────────────────────────────────────────────────────────────────┘
```

### 1.1 ワイヤ契約（`webui/bridge/transport/protocol.ts`、`Host/Boot/WebSocketEnvelope.cs`、`Host/Boot/WsClientMessage.cs`）

| 方向 | op | 中身 | 受け手 |
|---|---|---|---|
| Unity→Web | `snapshot` / `event` | `{topic, revision, data}`。revision は topic ごとにホストの生存中は単調増加 | `webSocketClient.ts` → `topicStore.setTopic`（保持中の revision 以下は捨てる） |
| Unity→Web | `result` | `{requestId, ok, error?}` | `pendingActions` の Promise を解決 |
| Unity→Web | `pong` | — | heartbeat |
| Web→Unity | `subscribe` / `unsubscribe` | `{topics[]}` | `WebSocketConnection.Topics` に登録してから snapshot を送る |
| Web→Unity | `action` | `{type, requestId, payload}` | action キュー → `IActionHandler` |
| Web→Unity | `input_state` | `{pointerOverUi, textInputFocused}` | `WebUiInputExclusivity.SetState`（WS の受信スレッドから呼ばれる） |
| Web→Unity | `ping` | — | `pong` を返す |

規約の正本は `docs/webui/topic-conventions.md`。購読登録の後に snapshot を作り、revision で順序の逆転と重複を捨てる。action は目標状態を指定する形にして冪等にする。再接続時は restoring で操作を塞ぐ。これらは**すべて WS を越える都合のための規約**で、同じプロセスで同期的に購読する UITK では不要になる。

### 1.2 起動と寿命の順序

| 順 | 場所 | 何をするか |
|---|---|---|
| 1 | `Scripts/Client.Starter/InitializeScenePipeline.cs:68-73` → `Initialization/Boot/WebUiStartup.cs` | `WebUiHost.StartAsync`: 本番は dist を検証。Kestrel をポート 25050 から空きを探して起動。開発時は Vite を起動。失敗しても「Web UI なしで続行」 |
| 2 | 同 `:73` | `RemoteExecActivation.ActivateIfRequested(webUiStarted, WebUiHost.KestrelPort)`。**遠隔実行は Kestrel のポートに相乗りしている** |
| 3 | `Scripts/Client.Starter/EventMode/EventModeStartGate.cs` | `EventLanguageGateBinder.Bind(hub, …)` と `LocalizationTopic` を先に登録し、出展モードでは言語選択まで開始を止める（DI より前） |
| 4 | `Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs:55-57` | `StartGame` → `new ClientDIContext` → `WebUiGameBinder.Bind()`（全 topic と action を登録） |
| 5 | `MainGameUI.prefab` の `WebUiCefNavigator`（`Host/Cef/WebUiCefNavigator.cs`） | 1 フレーム待ってから `LoadUrl`。WS 接続が確認できるまで最大 5 回再発行する |
| 6 | `GameShutdownEvent` → `WebUiHost.Stop` | アイコンキャッシュの破棄 → `ClearBindings`（topic と action を Dispose し、保留中のモーダルを cancel）→ WS を閉じる → Kestrel を停止 |

---

## 2. topic 一覧（Unity→Web）

区分:
- **A**: 正本は Web の外にあり、Topic は購読と DTO 化だけ。UITK は正本を直接購読すればよい。
- **A'**: A と同じだが、DTO の生成に表示用の合成（所持の集計、財布、アイコンや kind の解決）を含む。DTO ファクトリを UI に依存しない場所へ移せば共用できる。
- **B**: 状態・取得処理・単一消費の仲介を `Client.WebUiHost` の中に持つ。移設が必要。
- **C**: Web へ配送するための都合で、UITK では不要。
- **D**: 検証基盤専用。

| topic | C# の送り手 | 正本（読む先） | 配信のきっかけと頻度 | ペイロード（C# DTO ／ zod） | Web の受け手 | 区分 |
|---|---|---|---|---|---|---|
| `ui_state.current` | `Host/Game/Topics/UiStateTopic.cs` | `UIStateControl.CurrentState`、`UIStateDictionary.GetState().GetKeyHints()`、`INestedPauseScreenState.SubState` | `OnStateChanged`・入れ子ポーズの `OnPresentationChanged` → フレーム末 | `{state, subState?, keyHints[{keyNameKey,textKey}]}` ／ `UiStateDataSchema` | `app/App.tsx`（画面ルーティング）、KeyHintHud、HotbarPanel、KeyControlHintHud、`shared/uiState/activeLayer.ts` | A |
| `local_player.inventory` | `Topics/InventoryTopic.cs` | `LocalPlayerInventoryController`（Main・Grab）、`LocalPlayerEquipment` | `OnItemChange`・`OnInventoryRefreshed`・`OnSlotsOrSelectionChanged` → フレーム末 | `PlayerInventoryDto{mainSlots[], grab, equipment[], selectedEquipment, equipmentSelectionConfirmationRevision}` | InventoryPanel、EquipmentPanel、GrabOverlay、RecipeViewer、ItemListPanel、ResearchTreePanel、`features/inventory/slotActions.ts` | A |
| `block_inventory.current` | `Topics/BlockInventoryTopic.cs`（+`BlockDetail/*`・`Inventory/BlockInventoryDtoFactory.cs`・`TrainInventoryDtoFactory.cs`・`OpenTrainTimetableTracker.cs`・`TrainTimetableFetcher.cs`） | `UIStateControl`、`SubInventoryState`、`BlockGameObjectDataStore` → `BlockGameObject` の StateDetail、`BeltTransportClientCache`、`TrainUnitClientCache`、時刻表 lookup、サーバー問い合わせ（`BlockNetworkInfoCache`） | 開閉・スロット更新・**ブロック別の state イベント（Topic が直接購読）**・ネット取得の完了・ベルトの窓 → フレーム末。歯車ブロックは `IntervalFrame(4)` でサンプル | 判別 union: `{open:false}` ／ block `{blockType, identifier, blockGuid, itemSlots, fluidSlots, machine?, generator?, miner?, pump?, gear?, electricNetwork?, gearNetwork?, filterSplitter?, electricToGear?, gearToElectric?, trainPlatform?, trainStation?, beltThroughput?}` ／ train `{timetable: loading|unavailable|ready}` | BlockInventoryPanel 配下、slotActions、activeLayer | **B** |
| `crafting.recipes` | `Topics/CraftingRecipesTopic.cs` | `MasterHolder` のクラフトレシピ × `IGameUnlockStateData` | `UnlockedEventPacket` | `{recipes[{recipeGuid,resultItemId,resultCount,craftTime,requiredItems[]}]}` | RecipeViewer、ItemListPanel | A |
| `crafting.machine_recipes` | `Topics/MachineRecipesTopic.cs` | 機械レシピマスタ × 解放状態 | 同上 | `{recipes[{recipeGuid,blockGuid,blockId,time,inputItems,outputItems,inputFluids,outputFluids}]}` | BlockInventoryPanel、MachineSection、RecipeViewer | A |
| `recipe_viewer.item_list` | `Topics/RecipeViewerItemListTopic.cs` | `ItemRecipeViewerDataContainer` × 解放状態 | 同上 | `{itemIds[]}` | ItemListPanel | A |
| `ui.modal` | `Topics/ModalTopic.cs` | **`Host/Game/WebUiModalService.cs` の `Pending`** | `OnPendingChanged` → フレーム末 | `{modal?:{id,title,message,buttonText,variant,input?}}` | ModalHost、activeLayer | **B** |
| `ui.progress` | `Topics/ProgressTopic.cs` | `ProgressBarState` | 変化フラグ + 100ms 固定サンプル | `{visible, progress, label?}` | ProgressBar | A（サンプルは配信の都合） |
| `research.tree` | `Topics/Research/ResearchTopic.cs`（+`ResearchNodeDtoFactory.cs`） | 研究マスタ + **Topic 内の `_nodeStates`**（ResearchTree に入ったときと所持が変わったときにサーバーから再取得。`research.complete` の応答もここへ書く） | 再取得の完了 | `{nodes[{guid,state(5値),iconItemId,position,prevGuids,consumeItems,rewardItems,unlock*}]}` | ResearchTreePanel | **B** |
| `build_menu.entries` | `Topics/BuildMenu/BuildMenuTopic.cs`（+`BuildMenuEntryDtoFactory.cs`・`BuildMenuMaterialAvailability.cs`） | `PlacementTargetResolver`、`ClientBlueprintLibrary`、`ConstructionWalletQuery`、所持、サムネイル、buildMenu マスタ | ライブラリ・サムネ・財布・state、ビルドメニュー表示中に限り所持の変化 → フレーム末 | `{categories[], entries[kind 判別: block|trainCar|connectTool|blueprintCopy|blueprint, requiredItems{held,lacking}, paymentWaived, setPlacement?, iconUrl?]}` | BuildMenuPanel | A' |
| `local_player.hotbar` | `Topics/Hotbar/HotbarTopic.cs` | `ClientHotbarDatastore`、Resolver、BP ライブラリ、`PlaceSystemStateController`、サムネ、解放状態 | 各変化 → フレーム末 | `{slots[9]: null|{kind,id,iconUrl?,label?}, selectedSlot}` | HotbarPanel | A' |
| `localization.current` | `Topics/LocalizationTopic.cs` | `Localize`（言語コードと辞書の revision） | `Localize.OnLanguageChanged` | `{locale, revision}`。受けた Web が `/api/i18n/{locale}?revision=` を取りに行く | I18nProvider、LanguageSelect | C |
| `settings.sprintInputMode` | `Topics/Settings/SprintInputModeTopic.cs` | `SprintInputModePreference`（static） | 変化 | `{mode, recoveredInvalidPreference}` | SprintModeSelect | A |
| `settings.graphicsQuality` | `Topics/Settings/GraphicsQualityTopic.cs` | `GraphicsQualityPreference`（static） | 変化 | `{preset, settings{9項目}, recoveredInvalidPreference}` | GraphicsQualitySettings | A |
| `event_mode.language_gate` | `Host/Game/EventMode/EventLanguageGateTopic.cs` | **`Host/Game/EventMode/EventLanguageGate.cs`** | `OnWaitingChanged` | `{waiting}` | App.tsx → EventLanguageGate | **B** |
| `challenge.tree` | `Topics/Challenge/ChallengeTreeTopic.cs` ← `ChallengeTopicState.cs` | **`ChallengeTopicState._categories`**（handshake の初期値と `CompletedChallengeEvent` を Topic 側で保持） | 完了イベント | `{categories[{guid,iconItemId,nodes[{guid,iconItemId,state,position,scale,prevGuids}]}]}` | ChallengePanel | **B** |
| `challenge.current` | `Topics/Challenge/ChallengeCurrentTopic.cs` ← 同上 | 同上 | 同上（`completedChallengeGuid` は event だけに載る） | `{challenges[{guid,categoryGuid}], completedChallengeGuid?}` | CurrentChallengeHud | **B** |
| `pause_menu.current` | `Topics/C2/PauseMenuTopic.cs`（+`PlaytestDeliveryDto.cs`） | `NetworkDisconnectState`、`BugReportCaptureSession.Status`、`PlaytestDeliveryStatusBoard.Current`、`PauseMenuStateService.CurrentPage` | 各 ReactiveProperty の変化 | `{disconnected, bugReport(kind 4値+missing), delivery(kind 4値), page}` | PauseMenuPanel（購読はブリッジ初期化時に固定） | A |
| `ui.placement_mode` | `Topics/C2/PlacementModeTopic.cs`（+`PlacementModeDtoFactory`） | `PlaceSystemStateController`、`PlacementHeightOffset` | 各変化 | `selectedTargetType` で判別する union（block|connectTool|trainCar|blueprintCopy|raw）+`{height, unavailableReason, wheelOwnedByTool}` | PlacementModeHud、EquipmentPanel | A |
| `ui.crosshair` | `Topics/C2/CommonHudTopics.cs` | `CrosshairVisibility` | 変化 | `{visible}` | Crosshair | A |
| `ui.visibility` | 同上 | `UIRoot.OnVisibilityChanged` | 変化 | `{visible}` | App.tsx（全体の非表示） | A |
| `ui.tooltip` | `Topics/C2/TooltipTopic.cs` | `MouseCursorTooltipState`（ワールド上の物体のツールチップ） | 変化 | `visible` で判別: false なら行 0 件、true なら行 1 件以上 `{textKey,textParams[]}` | CursorTooltip | A |
| `game_state.current` | `Topics/GameStateTopic.cs` | `GameStateController`（static、InGame/Skit/CutScene） | 変化 | `{state}` | App.tsx（CutScene 中は全非表示） | A |
| `tutorial.presentation` | `Topics/Tutorial/TutorialPresentationTopic.cs` | `TutorialPresentationStateStore.Instance`（`Client.Game/InGame/Tutorial/Presentation/`） | 変化 | `{revision, sessions[{tutorialSessionId,challengeId,elements[outline|dragGuide|keyControl]}]}` | TutorialOverlay、KeyControlHintHud | A（anchor の解決は Web 側。§6.3） |
| `tutorial.world_pins` | `Topics/Tutorial/WorldPinTopic.cs` | `WorldPinStateStore.Instance`（各ピンの `Update` が毎フレーム `SetPin` し、変化したときだけ発火） | カメラが動いている間は毎フレーム | `{revision, pins[{pinId,tutorialGuid,screenX,screenY,onScreen,directionX,directionY}]}` | WorldPinOverlay | A（UITK なら同じフレームで投影できる） |
| `skit.presentation` | `Topics/SkitPresentationTopic.cs` | `SkitPresentationStateStore.Instance`（`Client.Skit/UI/`） | 変化 | `{sessionId, sceneRevision, presentationState{…, textReveal}, allowedIntents[]}` | SkitPresentation、SkitTransition | A |
| `train.riding` | `Topics/TrainRidingTopic.cs` | `UIStateControl`、`TrainHUDScreenState`、乗車イベント | 変化 | `{riding, branchCandidateCount, selectedBranchIndex}` | TrainRidingHud | A |
| `notification.events` | `Topics/Notification/NotificationTopic.cs`（+`NotificationCategoryTable.cs`） | `VanillaApi.Event`（`NotificationService.EventTag`）+`ClientLocalNotificationSource` | イベントの中継。**連番の採番と、移行告知だけを snapshot として保持**する処理は Topic 内 | `{}` ／ itemEarned `{seq,messageId,messageParams,itemId,count}` ／ message `{seq,category,messageId,messageParams,itemId?}` | NotificationHost | **B** |
| `playtest.dom_query` | `Scripts/Client.Playtest/WebUi/PlaytestDomQuery.cs`（`RegisterTopic` を通さず Publish だけ） | プレイテスト DSL | 要求ごと | `{requestId,testid}` | `webui/bridge/playtest/domQueryResponder.ts` | D |

Web 側で購読をブリッジの寿命に固定している topic（`readTopic` で命令的に読むため）: modal、blockInventory、uiState、inventory、pauseMenu（`webui/bridge/transport/webSocketClient.ts:163-171`）。

---

## 3. action 一覧（Web→Unity）

ハンドラの登録はすべて `Host/Game/WebUiGameBinder.cs:161-193`、`Host/Game/C4/C4WebUiRegistration.cs`、`Host/Game/Hotbar/HotbarWebUiRegistration.cs`、`Host/Game/Actions/Train/TrainWebUiActionRegistration.cs`、`Settings/SettingsActions.cs`、`Localization/LocalizationActions.cs`、`EventMode/EventLanguageGateActions.cs` で行われる。payload の型の正本は `webui/bridge/transport/actionContract.ts`（`ActionPayloads` と `ACTION_TYPES` が 44 件）。C# 側は 44 件に `playtest.dom_query_result` を足した 45 件。

| action | payload | C# ハンドラ（`Host/Game/Actions/`） | 作用する正本 | 主な拒否コード | Web の発行元 |
|---|---|---|---|---|---|
| `inventory.move_item` | `{from:SlotRef,to:SlotRef,count}` | `Inventory/InventoryActions.cs` MoveItem | `LocalPlayerInventoryController.TryMoveItem` | invalid_count / invalid_slot / denyReason | `shared/itemMove/playerSlotPlan.ts` |
| `inventory.split` | `{from}` | 同 SplitGrab | controller（半分を掴む。grab が空であることが条件） | grab_not_empty | 同上 |
| `inventory.split_drag` | `{slots[]}` | 同 SplitDrag | controller | — | `features/inventory/slotActions.ts`（SplitDragSession） |
| `inventory.collect` | `{slot}` | 同 Collect | controller | — | playerSlotPlan |
| `inventory.sort` | `{}` | 同 Sort | controller | — | InventoryPanel |
| `inventory.select_equipment` | `{index}` | `Inventory/EquipmentActions.cs` | `LocalPlayerEquipment.SetSelectedIndex` | invalid_index | EquipmentPanel（ホイール） |
| `block_inventory.move_item` | `{from:BlockSlotRef,to,count}` | `Inventory/BlockInventoryActions.cs` | controller.TryMoveItem（SubInventory と通し番号で合成） | 同上 | `shared/itemMove/blockSlotPlan.ts`、playerSlotPlan |
| `block_inventory.split` | `{from}` | 同 | 同 | grab_not_empty | blockSlotPlan |
| `block_inventory.collect` | `{slot}` | 同 | 同 | — | blockSlotPlan |
| `craft.execute` | `{recipeGuid}` | `CraftActions.cs` | 解放を確認 → `VanillaApi.SendOnly.Craft` | invalid_recipe / recipe_locked | CraftRecipeEntry（長押しで連続） |
| `research.complete` | `{researchGuid}` | `ResearchActions.cs` | `VanillaApi.Progression.CompleteResearch` → `ResearchTopic.ApplyNodeStates` | research_failed | ResearchDetailPane |
| `machine_recipe.select` | `{operation:set|clear, recipeGuid?}` | `MachineRecipeSelectionActions.cs` | `VanillaApi.Block.SendMachineRecipeSelectionRequest` | block_not_open / recipe_locked | MachineRecipeSelectionList |
| `filter_splitter.set_mode` | `{directionIndex, mode}` | `FilterSplitterActions.cs` | サーバー要求 → `BlockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot` | filter_request_failed | FilterSplitterInventory |
| `filter_splitter.set_filter_item` | `{directionIndex, slotIndex, clear}` | 同 | 同（grab しているアイテムを設定する） | 同 | 同 |
| `electric_to_gear.set_output_mode` | `{modeIndex}` | `ElectricToGearActions.cs` | `VanillaApi.Block.SetElectricToGearOutputMode` | invalid_block_type / mode_request_failed | ElectricToGearInventory |
| `train_platform.set_transfer_mode` | `{mode}` | `TrainPlatformActions.cs` | `VanillaApi.Train.SetTrainPlatformTransferMode` | 同上 | TrainPlatformSection |
| `train_station.set_name` | `{name}` | `Train/TrainStationActions.cs` | `VanillaApi.Train.SetTrainStationName` | — | TrainStationNameSection |
| `train_timetable.open` | `{}` | `Train/TrainTimetableOpenActionHandler.cs` | `TrainTimetableFetcher.RequestForOpenedTab` | — | TrainInventoryBody |
| `train_timetable.replace` | `{stops[{x,y,z,side}]}` | `Train/TrainTimetableActions.cs` | `VanillaApi.Train.SendTrainScheduleEdit` | — | `useImmediateTimetable.ts` |
| `train_timetable.set_auto_run` | `{enabled}` | 同 | 同 | — | TrainTimetableSection |
| `ui_state.request` | `{state: GameScreen|PlayerInventory}` | `UiStateActions.cs` | `UIStateControl.RequestTransition`（遷移許可表を通す）。入れ子ポーズのときは `RequestClosePauseMenu` | unsupported_state / transition_not_allowed | BlockInventoryPanel、**BlockInventoryKeyHandler（Esc）**、BuildMenuPanel |
| `ui.modal.respond` | `{id, result, text?}` | `ModalActions.cs` | `WebUiModalService.Respond` | no_pending_modal | ModalHost |
| `build_menu.select` | `{id}` | `BuildMenuActions.cs` | `BuildMenuSelection.SetSelectedTarget`（state が BuildMenu であることが条件） | invalid_state / unknown_entry | BuildMenuPanel |
| `blueprint.delete` | `{id}` | 同 | `ClientBlueprintLibrary` の削除 | blueprint_delete_not_found | BuildMenuPanel |
| `hotbar.select` | `{index}` | `Hotbar/HotbarActions.cs` | `ClientHotbarDatastore.EnqueueSelectRequest`（GameScreen と PlaceBlock のときだけ） | invalid_state | HotbarPanel |
| `hotbar.assign` / `hotbar.clear` / `hotbar.swap` | `{slot,id}` ／ `{slot}` ／ `{from,to}` | 同 | `RequestAssign` / `RequestClear` / swap | invalid_slot / invalid_id | `features/hotbar/hotbarDnd.ts`（割当はビルドメニューからの D&D だけで発生する） |
| `pause_menu.save` / `save_and_quit` / `show_page` | `{}` / `{}` / `{page}` | `PauseMenuActions.cs` | `GameSaveRequester.Save` ／ `SaveAndQuitPresenter` ／ `PauseMenuStateService.ShowPage` | invalid_page | PauseMenuTop/SubPage |
| `bug_report.submit` | `{description, kind}` | `BugReportSubmitActionHandler.cs` | `BugReportSubmitter.SubmitAsync`（Web 側のタイムアウトは 120 秒） | empty_description / invalid_kind | BugReportForm |
| `localization.setLocale` | `{locale}` | `Localization/LocalizationActions.cs` | `Localize.TrySetChosenLanguage` | unknown_locale | LanguageSelect |
| `event_mode.select_language` | `{locale}` | `EventMode/EventLanguageGateActions.cs` | `EventLanguageGate.TrySelectLanguage` | already_selected / unknown_locale | `useLanguageSelectionAnswer.ts` |
| `settings.setSprintInputMode` | `{mode}` | `Settings/SettingsActions.cs` | `SprintInputModePreference.SetMode` | unknown_sprint_mode | SprintModeSelect |
| `settings.applyGraphicsQualityPreset` / `settings.setGraphicsQuality` | `{preset}` / `{item, value}` | `Settings/GraphicsQualityActions.cs`（+`GraphicsQualityWire.cs`） | `GraphicsQualityPreference.ApplyPreset / SetSettings` | unknown_graphics_quality_preset | GraphicsQualitySettings |
| `tutorial.anchor_ack` | `{tutorialSessionId, revision, elementId, anchorId, status, reason}` | `TutorialAnchorAckAction.cs` | 照合だけで副作用は無い（session と revision が古いと失敗） | stale_session / stale_revision | TutorialOverlay |
| `skit.advance` / `select` / `set_auto` / `skip` / `set_ui_hidden` | `{sessionId, sceneRevision, …}` | `Skit/SkitActionHandlers.cs` | `SkitPresentationStateStore.Instance` | invalid_payload | SkitPresentation / SkitToolbar / SkitChoiceList |
| `debug.echo` | `{hello}` | `DebugActions.cs`（Editor と DEVELOPMENT_BUILD のみ） | なし | — | 呼び出し元なし |
| `playtest.dom_query_result` | DOM の矩形と可視判定 | `Scripts/Client.Playtest/WebUi/PlaytestDomQuery.cs` | プレイテストの DOM 問い合わせへの応答 | — | `webui/bridge/playtest/domQueryResponder.ts` |

action ハンドラの多くは「JSON の解析 + 前提条件の検証（state 一致・解放判定・遷移許可表）+ 正本の操作」を 1 つにまとめて持つ。前提条件の検証は UITK でも同じものが要るので、**型付きのコマンド関数（UI に依存しない）と JSON アダプタに分ける**と、共存中に両系から同じ検証を通せる。例: `BuildMenuActions.cs:39`（state==BuildMenu）、`HotbarActions.cs:34`、`UiStateActions.cs:37-50`、`CraftActions.cs:37-38`。

---

## 4. HTTP エンドポイント（`Host/Boot/WebUiEndpoints.cs`）

| パス | 実装 | 中身 | UITK での扱い |
|---|---|---|---|
| `/ws` | 同ファイル `:37-86`（Origin を検査し、手書きで RFC 6455 の handshake） | WebSocket | 撤去 |
| `/api/ping` | 同 `:88` | 疎通確認 | 撤去 |
| `/api/remote-exec` | `Scripts/Client.RemoteExec/RemoteExecEndpoint.cs` | 認証つき遠隔実行（Harmony 等） | **Kestrel に相乗りしている。待受を別に用意する必要がある**（§7） |
| `/api/i18n-languages` | `Host/Game/LocalizationLanguagesEndpoint.cs` | 言語コードと表示名 | `LanguageSelection` を直接読む |
| `/api/i18n/{locale}?revision=` | `Host/Game/LocalizationDictionaryEndpoint.cs` | `Localize` の辞書（source は擬似 locale）。世代が違えば 409 | `Localize.Get*` を直接呼ぶ |
| `/api/master/items` | `Host/Game/ItemMasterEndpoint.cs` | itemId→itemGuid と maxStack | `MasterHolder` を直接読む |
| `/api/master/fluids` | `Host/Game/FluidMasterEndpoint.cs` | fluidGuid→color | 同上 |
| `/api/icons/`・`/api/block-icons/`・`/api/fluid-icons/`・`/api/train-car-icons/`・`/api/connect-tool-icons/`・`/api/blueprint-icons/` + `{key}.png` | `Host/Game/Icons/IconEndpoint.cs` + `IIconTextureSource` 実装 6 種 | Texture2D を PNG にエンコードしてキャッシュ | 解決部分（`ResolveOrNull`）だけ残し、Texture2D や Sprite を直接使う。文中アイコンは PoC のとおり実行時に作る SpriteAsset（内部 API に依存） |
| `/api/assets/*` | `Host/Assets/GenericImageAssetEndpoint.cs` | 汎用画像 | 直接参照 |
| 本番の静的配信 | `Host/Static/*`（成果物の hash 照合） | dist | 撤去 |

---

## 5. 層の切り分け（どこが正本で、どこが Web 専用か）

```
[正本・残す]                       [Web 専用の変換・配信層・撤去]                    [Web 側ロジック・C# へ移植]
Client.Game (UIState, Inventory,   Client.WebUiHost                                  moorestech_web/webui/src
  Hotbar, PlaceSystem, Tutorial,     Boot/ Kestrel・WS・envelope・dispatcher           features/*/logic・shared/*
  BugReport, PauseMenu ...)          Common/ Static/ Vite/ Editor/ Cef/               （集約・整形・文言解決・計画・導出）
Client.Skit (SkitPresentationStore)  Game/Topics・Actions の JSON 部分                 bridge/（転送・検証）← 移植は不要
Client.Input (Sprint, Exclusivity)   Game/Icons・*Endpoint（PNG と JSON の配信）
Client.Localization (Localize)     ─────────────────────────────
Game.* / VanillaApi（サーバー）      ★Client.WebUiHost の中にある「状態と取得」（§5.2）→ 撤去前に移設
```

### 5.1 正本はそのままで、配信を C# の購読に変えるだけで済む部分（区分 A と A'）

`ui_state.current`、`local_player.inventory`、`crafting.recipes`、`crafting.machine_recipes`、`recipe_viewer.item_list`、`ui.progress`、`build_menu.entries`（A'）、`local_player.hotbar`（A'）、`settings.*`（2 件）、`pause_menu.current`、`ui.placement_mode`、`ui.crosshair`、`ui.visibility`、`ui.tooltip`、`game_state.current`、`tutorial.presentation`、`tutorial.world_pins`、`skit.presentation`、`train.riding`。

やり方の提案（推測を含む設計案）:
- 各 `XxxTopic` を「型付き DTO を作る部分 + 変化通知（`IObservable<Unit>`）」と「JSON アダプタ」に分ける。DTO クラスは既に public（例: `InventoryTopic.cs:110-123` の `PlayerInventoryDto` と `SlotDto`）なので、分割は機械的にできる。UITK のビューは前者を購読する。後者は CEF を撤去するときに消す。
- フレーム末に畳む処理（PostLateUpdate、INFRA-7）は UITK でも役に立つ（中間状態を描かない）。100ms や IntervalFrame(4) のサンプリングは WS の帯域のためなので、UITK では毎フレーム直接読んでよい。
- **revision・snapshot・restoring・再接続・heartbeat・zod の検証は、同じプロセスで同期的に購読するなら全部いらなくなる。** zod に込めた不変条件は、C# の型（判別 union は sealed class や record の継承）で表し直す。対象は FluidSlot の empty/filled 判別（`webui/bridge/contract/schemas/inventory.ts:27-41`）、変換機詳細の排他（同 `:194-203`）、Tooltip の visible と行数の対応（`schemas/ui.ts:141-150`）、Hotbar の 9 枠固定（`schemas/hotbar.ts:45-48`）、PlacementMode の判別（`schemas/ui.ts:102-129`）。

### 5.2 `Client.WebUiHost` の中にある状態と取得処理（撤去前の移設が必須）

| クラス | 持っているもの | 移さないと何が困るか |
|---|---|---|
| `Host/Game/Topics/Research/ResearchTopic.cs` | 研究ノード状態 `_nodeStates`。ResearchTree に入ったときと所持が変わったときにサーバーから再取得する。`research.complete` の応答もここへ書く | クライアントで研究状態を持つ場所がここしか無い |
| `Host/Game/Topics/Challenge/ChallengeTopicState.cs` | チャレンジの全量状態（handshake + `CompletedChallengeEvent`） | 同上（チャレンジの tree と current の唯一の保持先） |
| `Host/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs` | 開いているブロックの電力網と歯車網を 1 秒ごとにポーリング。フィルタ分配器の状態を 1 回取得 | 詳細パネルの網情報の出所 |
| `Host/Game/Topics/BlockInventoryTopic.cs` の `TrackBlock` | 開いたブロックのブロック別 state イベントの購読、連続値のサンプル判定、ベルト窓での再配信 | 「開いているブロックの何を監視するか」の判断が Topic の中にある |
| `Host/Game/Topics/Inventory/OpenTrainTimetableTracker.cs`、`TrainTimetableFetcher.cs` | 時刻表の取得状態機械（loading / unavailable / ready） | 列車インベントリの時刻表タブ |
| `Host/Game/Topics/Notification/NotificationTopic.cs` | 連番の採番。接続時に 1 回だけ届く saveMigration 告知を初期データとして保持 | 通知の重複を防ぐ仕組みの前提 |
| `Host/Game/WebUiModalService.cs` | モーダル要求（同時に 1 件）。`static Instance` になっているのは「Client.Game から Client.WebUiHost を参照できない」ため（同ファイル冒頭のコメント） | ゲーム側からモーダルを出す唯一の口 |
| `Host/Game/BlueprintNameInputWebBridge.cs` | `BlueprintNameInputState` → 入力モーダル → 状態への書き戻し | BP 名入力 |
| `Host/Game/EventMode/EventLanguageGate.cs`（+Binder） | 出展モードの言語選択ゲート。`Client.Starter` が `WaitForSelectionAsync` で待つ | DI より前に表示する必要がある |
| DTO ファクトリ群（`BlockDetail/*DtoBuilder.cs`、`MachineSlotBindingDtoBuilder.cs`〈「ホストが配信する正本」とコメントがある〉、`BuildMenuEntryDtoFactory.cs`、`BuildMenuMaterialAvailability.cs`、`ResearchNodeDtoFactory.cs`、`ChallengeDtoBuilder.cs`、`PlacementModeDtoFactory`、`TooltipTopic.ToDto`、`NotificationCategoryTable.cs`、`TrainTimetableDtoBuilder.cs`、HotbarTopic のスロット解決） | 表示用に確定させた値（束縛、不足、財布、kind、アイコン） | UITK でも同じ確定値が要る |

**生存条件の罠**: 上の状態クラスは `WebUiGameBinder.Bind()` の中でしか `new` されない。`Bind` は `WebUiHost.Hub == null` のとき警告を出して全部スキップする（`Host/Game/WebUiGameBinder.cs:48-55`）。UITK がこれらに依存するなら、Hub と無関係に DI（MainGameStarter の LifetimeScope）へ登録する場所へ移すのが前提になる。移設先が `Client.Game` なら、`WebUiModalService` が static である理由（依存の向き）も消える。

### 5.3 Web 側にしか無いロジック（C# へ移植が要るもの）

非テストの `.ts` は全 141 ファイルで、そのうち `features`、`shared`、`app` の合計が約 4,010 行（`find … | xargs wc -l` の実測）。`.tsx` は 116 ファイル、CSS は 3,407 行。

| 分類 | Web のファイル | 中身 | C# の現状 | 移植先の案 |
|---|---|---|---|---|
| 文言解決 | `webui/shared/i18n/i18nStore.ts`、`preDictionaryText.ts`、`generated/*` | 3 段のフォールバック（locale→english→source）、**名前付き補間 `{itemName}`**、欠けたキーを `[!key]` で表示、辞書の到着前に使う文言表、未知の外部キーへの警告 | 3 段のフォールバックは `Scripts/Client.Localization/LocalizationTextResolver.cs` に同じものがある。**補間は `{p0}` の位置引数だけ**（`LocalizationTextInterpolator.cs`）。`Localization/localization.csv` のうち **44 キーが名前付きプレースホルダ**（例: `ui.buildMenu.materialTooltip`、`ui.notification.itemEarned`、`ui.blockInventory.*`、`ui.bugReport.delivery*`） | C# に名前付き補間を足すか、44 キーを `{pN}` に直す（mod の翻訳にも及ぶ）。どちらにするか裁定が要る |
| 通知の文言表 | `webui/features/notification/notificationMessages.ts` | messageId→翻訳キーの表（約 70 件）、GUID 引数→content key（研究名・チャレンジ名）、未知 ID を専用キーで見せる処理 | なし（サーバーは構造化 ID だけを送る） | 通知のプレゼンタへ移す |
| 通知の集約 | `webui/features/notification/notificationStore.ts` | 獲得通知を同じ itemId と messageId の表示中の行へ合算、lifetimeEpoch、7 秒の生存、saveMigration の seq を既読管理 | なし。「集約は Web 側で行う」という裁定がある（`.decisions/2026-08-19-獲得通知の集約はWebUI側で行う.md`） | UITK の通知ビューモデルへ。裁定の置き場所を書き直す必要がある |
| action 失敗のトースト | `webui/bridge/transport/actions.ts`（`BENIGN_ERRORS`）、`features/toast/toastStore.ts` | 古い状態から来る良性の失敗は黙らせ、それ以外はトーストする | ハンドラは error code の文字列を返すだけ | 型付きコマンドの結果 enum で分岐する。トーストを残すかどうかは再裁定 |
| 画面ルーティングと層 | `webui/shared/uiState/uiScreenRouting.ts`（SCREEN_TRAITS: grab / pauseMenu / skitInput / backdrop / alwaysOnHud / trainHud）、`activeLayer.ts`（modal > blockInventory > research > buildMenu > game） | UIState → 描く画面と HUD の対応、最前面の層 | UIStateEnum と遷移だけ | UITK の画面切替表として C# に 1 つ置く（共存期の描画者の表を兼ねる。§8.4） |
| 操作の計画 | `webui/shared/itemMove/*`（`planDirectMoves`、`playerSlotPlan`、`blockSlotPlan`）、`features/inventory/splitDrag.ts`、`slotActions.ts`（機械の入力帯を束縛で絞る）、`shared/pointerGesture/*`、`features/hotbar/hotbarDnd.ts` | 左・右・ダブルクリック・Shift 配分、分割ドラッグ、ドラッグの閾値、長押し、ホットバー D&D の割当・入替・解除 | 単発の `TryMoveItem` などだけ（uGUI 時代の `PlayerInventoryDirectMover` は grep で見つからず、撤去済み） | UITK のスロット操作コントローラへ。vitest が仕様になる |
| 集計と導出 | `shared/ownedCounts.ts`、`features/recipe/logic/craftLogic.ts`（作れる数）、`research/researchLogic.ts`（ボタン・カードの状態と初期フォーカス）、`buildMenu/logic/*`（カテゴリの群化・検索・不足）、`blockInventory/details/detailLogic.ts`（充足率・毎分個数・帯分割・燃料比・状態表示の表・歯車の行）、`beltThroughputLogic.ts`、`machine/machineSlotGhosts.ts`、`machine/machineRecipeSelectionLogic.ts`、`train/timetableEditLogic.ts`、`filterSplitterLogic.ts`、`inventory/EquipmentPanel/equipmentLogic.ts`（循環とホイールの累積）、`recipe/logic/holdCraftLogic.ts`、`shared/ui/slotAmountFormat.ts`、`shared/materialTooltipText.ts` | 表示用の導出と整形 | なし | 純関数なので 1 対 1 で移植できる。EditMode NUnit へ |
| 楽観更新とセッション状態 | `blockInventory/train/useImmediateTimetable.ts`（ack と配信の両方を待つ）、`recipe/logic/selectionStore.ts`、`buildMenu/sessionState/*`、`pauseMenu/useBugReportDraft.ts`、`eventLanguageGate/useLanguageSelectionAnswer.ts`、スキットのタイプライタ | 画面のローカル状態 | なし | UITK のビュー側 |
| 座標とレイアウト | `shared/tooltip/tooltipPosition.ts`、`shared/treeView/*`（パン慣性とズーム）、`app/App.tsx` の stage 1280×720 一様拡縮（ADR 0013/0017）、`uiScaleStore` | 表示の配置 | なし | PanelSettings と自作 |
| チュートリアル anchor | `shared/tutorialAnchor/*`（DOM の `data-tutorial-anchor` を解決、祖先の clip、可視判定、ack の理由）、`anchorIds.ts` | 枠線とドラッグガイドの対象を探す | C# は anchorId を素通しするだけ。**マスタ `VanillaSchema/challenges.yml:280-337` の `highLightAnchorId` / `fromAnchorId` / `toAnchorId` が Web の anchor ID を直書きしている**（裁定 2026-08-19） | UITK 側の anchor レジストリ（VisualElement の名前やクラス）。ID の語彙を保つか、schema とマスタを改名するかを裁定 |
| マスタの取得 | `bridge/store/itemMasterStore.ts`、`fluidMasterStore.ts`、`languageList/*` | HTTP の再試行と再接続への追従 | `MasterHolder` と `LanguageSelection` を直接読める | 移植不要 |

### 5.4 移植しなくてよいもの（転送専用で消える）

`webui/bridge/transport/webSocketClient.ts`、`subscriptionManager.ts`、`store/topicStore.ts`（revision の照合、restoring、再接続オーバーレイ）、`contract/validators.ts` と `schemas/*`（不変条件は §5.1 のとおり型へ移す）、`httpEndpoints.ts`、`useWebInputExclusivity.ts` の送信部分、`Host/Common/WebUiJson.cs`、`docs/webui/topic-conventions.md` の WS に関する規約一式。

---

## 6. 撤去対象と依存関係

### 6.1 撤去リスト

| # | 対象 | 中身と根拠 |
|---|---|---|
| 1 | `moorestech_web/`（webui、e2e、mock-host、node toolchain） | `.tsx` 116、非テストの `.ts` 141、CSS 3,407 行、vitest 145 ファイル、e2e spec 53 本。`e2e/mock-host/inventoryModel.ts` などに action の再実装がある |
| 2 | `Scripts/Client.WebUiHost`（asmdef、134 ファイル、`.cs` 合計 10,565 行） | Boot（Kestrel・WS）、Cef（Navigator）、Common（ポート 25050+20、`MOORESTECH_WEBUI_MODE`）、Static（dist の検証）、Vite（dev サーバーの監視と pnpm）、Editor（`WebUiProductionArtifactBuilder` が IPreprocessBuild で pnpm build を実行、`WebUiToolchainBootstrap`、`EnvironmentScrubOnEditorLoad`）、Assets、Game。**ただし §5.2 は移設してから** |
| 3 | `jp.juha.cefunity`（`moorestech_client/Packages/manifest.json:45`）と、`Asset/UI/Prefab/MainGameUI.prefab` の CEF Canvas・RawImage・`CefUnityBrowserSample`・`WebUiCefNavigator` | prefab は `uloop execute-dynamic-code` を通して変更する（AGENTS.md） |
| 4 | `Scripts/Editor/Build/Bundlers/CefRuntimeBundler.cs`、`BuildPipeline.cs:90` の呼び出し | 合わせて `BuildPipeline.cs:62-71` の「CEF の Mac ランタイムが arm64 のみなので arm64 に固定」の理由が消える |
| 5 | `Scripts/Editor/Cef/CefPackageLfsValidator.cs`、`CefLfsPointer.cs` | LFS の検証ゲート |
| 6 | `scripts/setup-cef.sh`、`scripts/setup-cef.ps1` | |
| 7 | `.github/workflows/build.yml` | CEF の事前取得（234-246）、pnpm の準備（277-300）、Linux クライアントを外している理由（185） |
| 8 | `scripts/playtest/lib/release-artifact.sh:54-62`（CEF helper の実在チェック）、`scripts/playtest/tests/lib/release-playtest-build-stubs.sh`、`test-release-playtest-mac.sh` | |
| 9 | NuGet の AspNetCore 系（`Assets/Packages/Microsoft.AspNetCore.*` の DLL 24 個。`Assets/packages.config` の 74 件中 47 件が AspNetCore または Microsoft.Extensions） | **`Client.RemoteExec` が Kestrel の `HttpContext` を使っている**（`RemoteExecEndpoint.cs`、`WebUiEndpoints.cs:97-103`）。Microsoft.Extensions.* を ModelContextProtocol などが共有している可能性がある（要確認） |
| 10 | `Scripts/Client.Playtest/WebUi/`（CefInputForwarder、CefScreenMapper、PlaytestDomQuery）、`Operations/PlaytestWebUiOps.cs` ほか | 録画シナリオ 29 本が `ClickWebUi` や testid 系に依存（`.agents/skills/unity-playmode-recorded-playtest/scenarios/`） |
| 11 | `Scripts/Client.Tests` のうち `Client.WebUiHost` を参照する 65 ファイル（`WebUi/` 48、`WebUiHost/` 4 ほか）、`WebUi/WireFixtures/`（vitest と共用） | `WebUi/Gate/WebUiGateClassification.cs` と `WebUiGateAuditTest.cs` は ADR 0052 で「uGUI 追加禁止の安全網」として残しているので、対象を付け替える |
| 12 | `Scripts/Client.Input/WebUiInputExclusivity.cs` | 撤去ではなく汎用化と改名（§8.3） |
| 13 | `com.singtaa.onejs`（`manifest.json:13`） | 裁定で不採用になった残骸 |
| 14 | ドキュメントとスキル | `docs/webui/*`（topic-conventions、anchor-convention、MIGRATION、design/*）、`ref-webui-design` スキル、moores-code-review の webui ts/tsx 観点、`docs/development/macmini/unity-editor.md` の CEF 節、ADR 0052 の例外 1（CEF 描画面）。`com.unity.ugui` は例外 2〜4（MainMenu、ロード画面、HP バー、デバッグ UI）のために残る |
| 15 | `Client.Game` などに残る Web という名前 | `WebPinId`（13 箇所）、`SkitPresentationMode.WebUiEnabled`（`Client.Game/Skit/SkitManager.cs:197` で常に true。旧 UITK の `Client.Skit/UI/SkitUI.cs` と `Asset/UI/NewUI/Skit/SkitUI.uxml` の経路は使われないまま残っている）、`LocalizationKeys.Ui.Blueprint.WebModal*` など |

### 6.2 依存関係

```
moorestech_web/webui ──(WS / HTTP)──▶ Client.WebUiHost ─┬─▶ CefUnity.Runtime  (jp.juha.cefunity)
                                                        ├─▶ Nuget: Microsoft.AspNetCore.*（Kestrel）
                                                        ├─▶ Client.RemoteExec ──▶ AspNetCore HttpContext
                                                        ├─▶ Client.ExternalProcess（Vite と pnpm。Editor/Build の MacAppAdHocSigner と FfmpegRuntimeBundler も使うので残す）
                                                        └─▶ Client.Game / Client.Skit / Client.Input / Client.Localization / Game.* （正本）
Client.Starter  ──▶ Client.WebUiHost（WebUiStartup、InitializeScenePipeline:73 の KestrelPort、Finalizer:57 の Bind、EventModeStartGate）
Client.Playtest ──▶ Client.WebUiHost, CefUnity.Runtime, CefUnity.Interop
Client.Tests    ──▶ Client.WebUiHost（65 ファイル）
Editor/Build    ──▶ CefRuntimeBundler ──▶ jp.juha.cefunity
CI build.yml    ──▶ cef-unity の git clone と LFS、pnpm install
```

### 6.3 撤去順の制約（この観点からの提案）

1. **RemoteExec を Kestrel から外す**。`HttpListener` などの専用待受に移すか、RemoteExec 専用に最小の Kestrel を残すかは裁定。NuGet を整理できるかがこれで決まる。
2. **§5.2 の状態クラスを Hub と無関係な DI 登録へ移す**。挙動は変えない。単独の PR にできる。
3. **プレイテスト DSL に UITK 版の UI 操作を足す**。VisualElement の名前で検索し、イベントを注入する。DOM 版と並べて置き、各画面を移すときにシナリオを付け替える。
4. 画面ごとに UITK へ移す（§8）。
5. 全画面を移し終えたら、次の順で消す。`Client.Starter` の起動配線 → `Client.WebUiHost` → `jp.juha.cefunity` と prefab の CEF 面 → `CefRuntimeBundler`、LFS、CI、setup-cef → NuGet → `moorestech_web` → docs と skills → Web という名前の改名。
6. 副産物（裁定事項）として次が可能になる。Mac の arm64 固定の解除（Intel や universal 対応）、Linux クライアントのビルド再開（`build.yml:185`）、`Play` を止めるたびに cef-unity-server がゾンビ化する問題と FD 継承の消滅。

---

## 7. 画面単位で共存させる場合

### 7.1 同じ topic を両系が購読できるか

- **できる。** 正本は UniRx の `Subject` や `ReactiveProperty`、C# の event なので多購読に対応している。UITK のビューが正本を直接購読しても、Web 向けの Topic はこれまでどおり動く。
- `WebSocketHub` は JSON 文字列を WS 接続へ送るだけで、プロセス内から型付きで購読する口を持たない（`WebSocketHub.Publish` は `conn.Topics` にしか送らない。`Host/Boot/WebSocketHub.cs:122-135`）。UITK が Hub の JSON を逆変換して使うのは二重変換になる。§5.1 のように DTO と変化通知の部分を切り出して両系で共有するのが筋。
- **多購読にすると壊れる単一消費者**:
  - `WebUiModalService` は保留を 1 件しか持たず、応答も 1 回で終わる。両系が描くとモーダルが 2 枚出る。モーダルの描画者は 1 つに決める。
  - `BlueprintNameInputWebBridge` は状態を Web のモーダルへ自動で送る。UITK 版を作るときはこのブリッジを外す。
  - スキットの入力（`skit.advance` など）。両系が描画して両方が入力を受けると二重に送られる。
  - チュートリアル anchor。枠を描く系は「anchor が置かれている系」になる。片方の系が anchor を見つけられないと `not-found` の ack を返す。
- 取得のきっかけは画面に依存しない。`ResearchTopic` の再取得も `BlockNetworkInfoCache` のポーリングも UIState の遷移が駆動しているので、UITK が描いても同じように動く。ただし Topic インスタンスが生きていること（Hub の起動成功）が条件なので、§5.2 の移設が先に要る。

### 7.2 横断レイヤーと画面の同居

`webui/app/App.tsx` で常にマウントされている横断要素: GrabOverlay、CursorTooltip（`ui.tooltip`）、NotificationHost、ToastHost、ModalHost、TutorialOverlay、WorldPinOverlay、KeyHintHud、KeyControlHintHud、SkitPresentation と Transition、Crosshair、ProgressBar、CurrentChallengeHud、HotbarPanel と EquipmentPanel（`screenShowsAlwaysOnHud`）、再接続オーバーレイ、BlockInventoryKeyHandler、RecipeSelectionKeyHandler。

画面の組み合わせから来る制約:
- **InventoryPanel は playerInventory・subInventory・researchTree の 3 画面に出る**（`screenAllowsGrab`、`uiScreenRouting.ts:18-31`）。インベントリだけを UITK にすると、ブロックインベントリ（Web）や研究（Web）と同じ画面に並ぶ。grab の正本は C# の `GrabInventory` なので、系をまたいだ移動そのものはできる。しかし次の 3 つはパネルの状態をまたいでいる。カーソルに追従する掴み絵（GrabOverlay）、Shift 配分の計画（`playerSlotPlan.ts` がブロック側のスロットを読む）、機械の入力帯の束縛による候補の絞り込み（`slotActions.ts:93-101`）。分割ドラッグがパネルをまたぐ操作は、系をまたぐと成立しない。
- **ホットバーの割当は、ビルドメニューのエントリからの D&D だけで発生する**（`features/hotbar/hotbarDnd.ts` 冒頭のコメント、`buildMenu/views/BuildMenuSlot.tsx`）。DOM のドラッグを UITK の要素へ落とすことはできないので、**ホットバーとビルドメニューは同時に移す必要がある**。
- ResearchTreePanel は `local_player.inventory` を読んで所持数を表示している。

### 7.3 入力の取り合い（現状の裁きと、共存時に足す裁き）

| 入力 | 現状の経路と裁き場所 | UITK と共存すると起きること | 裁く場所の提案 |
|---|---|---|---|
| マウスのクリックと移動 | cef-unity が `Input.mousePosition` を毎フレーム無条件に CEF へ送る。RawImage は全面で、止める API は無い（`CefUnityBrowserSample.cs:553-590, 975-1125`）。DOM がヒット判定し、`pointermove` から `input_state.pointerOverUi` を送る → `UiPointerHitTest.IsPointerOverAnyUi()` = `EventSystem.IsPointerOverGameObject() || IsPointerOverWebUi`（`Client.Game/InGame/Control/UiPointerHitTest.cs`、**消費者は 23 ファイル**） | UITK パネル上のクリックも CEF に届く。下が DOM の透明面（`data-web-ui-transparent`）なら害は無いが、同じ位置に Web パネルがあると Web も反応して二重に発火する。Web は UITK を知らないので `pointerOverUi=false` を送ってくる | (1) 同じ画面位置に、両系の操作可能な要素を置かない（描画者を UIState と HUD 部品ごとに 1 つにする）。(2) `UiPointerHitTest` に UITK のピック（`RuntimePanelUtils.ScreenToPanel` + `IPanel.Pick` で、pickingMode=Position の要素）を足す。シーンに EventSystem があるので、UITK パネルは PanelRaycaster を通して `IsPointerOverGameObject` に乗る可能性もある（**推測・要実測**） |
| ホイール | CEF はネイティブのスクロールソースか `mouseScrollDelta` を送る。Web の `useGameLayerWheel`（EquipmentPanel の装備切替）は activeLayer が game のときに発火する。`data-wheel-passthrough` で素通しを指定 | UITK のスクロールビュー上でも CEF にホイールが届き、Web の装備切替が回る可能性がある | 装備 HUD をどちらの系に置くかと一緒に決める |
| キーボード（ゲーム操作の抑止） | Web のテキスト欄にフォーカスがある間は `input_state.textInputFocused` → `WebUiInputExclusivity` が `InputManager` と `HybridInput` のキーボード入力を 0 にする（`Client.Input/InputManager.cs:168-183`、`HybridInput.cs:96`）。加えて `TextInputMovementLockApplier`（`PlayerSystemContainer.cs:37`） | **UITK の TextField にフォーカスがあってもゲームのキーは止まらない**（Web から来た値しか見ていない）。CEF にも文字が送られる（DOM にフォーカスが無ければ害は無い） | `WebUiInputExclusivity` を「テキスト入力の所有者の合成」（Web の値 OR UITK の `FocusController.focusedElement is TextField`）に一般化し、改名する |
| Web 側でページ全体に張ったキー処理 | `BlockInventoryKeyHandler`（Esc → `ui_state.request` GameScreen）、`BeltThroughputSection`（keydown）、`RecipeSelectionKeyHandler`（Esc で選択を解除）、`useWebInputExclusivity`（Tab と Space の既定動作を封じ、Esc で blur） | 画面を UITK に移しても、App.tsx に常駐する handler が Web の topic を見て動き続ける | 画面を移す PR で Web 側の handler も外す。Esc は C# の UIState 側に一本化する（現状は Web と UIState の両方に Esc の経路がある） |
| IME | CEF は `Input.imeCompositionMode = On` にして `compositionString` を CEF へ送る（`CefUnityBrowserSample.cs:800-930`） | UITK の TextField も同じ `Input.imeCompositionMode` と `compositionString` を使う。共存中に取り合う可能性がある（**推測・要実測**） | テキスト欄を持つ画面（BP 名入力、駅名、ビルドメニューの検索、バグ報告、モーダルの入力）を移すときに、実機の IME で確かめる |
| フォーカス | DOM の `activeElement` と UITK の `FocusController` は互いに独立 | 両系が同時にフォーカスを持ち得る | テキスト欄を持つ画面は、テキスト欄ごとまとめて一度に移す |
| カーソルロック | ロック中の `UiPointerHitTest` は常に false | 同じ | 変更なし |
| プレイテスト入力 | `CefInputForwarder` が InputSystem に注入した値を CEF へ転送する。`PlaytestUiOps.cs:138` は `IsPointerOverWebUi` が落ちるのを待つ | UITK は InputSystem の注入を EventSystem 経由で受けられる見込み（**推測**） | DSL に UITK 版の `ClickUi(name)` などを足す（§6.3 の 3） |

### 7.4 描画順

CEF は uGUI Canvas の RawImage（`MainGameUI.prefab`）に描かれ、UITK は PanelSettings の sortingOrder で順序が決まる。Screen Space Overlay の Canvas と UITK パネルの前後関係はまだ確かめていない（**推測**: Unity 6 では同じオーバーレイ段で描かれ、sortingOrder で順が決まるとされる）。ツールチップ、掴み絵、モーダル、チュートリアル枠を UITK に置いたとき、それが Web パネルの上に出るかどうかがここで決まる。第一弾で実測する項目。

### 7.5 共存の規則（この観点からの推奨）

- **R1 描画者は UIState と HUD 部品ごとに 1 つ。** 画面を移す PR では、Web 側の該当パネルと全域キー処理を**同じ PR で削除**する。実行時に切り替えるフラグは作らない。前例として `SkitPresentationMode.WebUiEnabled` は DI に登録した bool だが常に true のままで、旧 UITK の SkitUI 経路が使われないまま残っている。切替フラグは残骸になりやすい。
- **R2 正本には手を入れない。** Topic を「DTO と変化通知」と「JSON アダプタ」に分け、UITK は前者を購読する。action は「型付きコマンド」と「JSON アダプタ」に分ける。
- **R3 状態クラスは Hub と無関係に生成する**（§5.2 の移設を共存開始の前に行う）。
- **R4 入力の裁きは `UiPointerHitTest` と（改名後の）入力排他の 2 か所に集める。** 個々の画面で入力の判定を持たない。
- **R5 同時に表示される面はまとめて移す。** インベントリの面（playerInventory・subInventory・researchTree）と GrabOverlay、ホットバーとビルドメニュー、テキスト欄を持つ画面とその IME。

---

## 8. 未裁定の論点に対する材料（この観点から）

| 論点 | 材料 |
|---|---|
| 移行中の出荷形 | 画面単位の strangler で進められる。ただし単位は UIState 1 つではなく「同時に表示される面の集合」（§7.2、§7.5 R5）。CEF は最後の画面を移すまで配布物に残る。共存中は入力の二重発火を R1 と R4 で塞ぐ |
| 仕様の正本 | vitest のうち §5.3 の純関数ロジック（計画・導出・整形・ルーティング表）は、C# の EditMode NUnit へほぼ 1 対 1 で移せるので、移植の優先度が高い。`WireFixtures` を使う契約テスト（`Client.Tests/WebUi/WireContract*Test.cs` と `webui/bridge/contract/wireContract*.test.ts`）は JSON アダプタと一緒に消える。e2e の mock-host の action 再実装（`e2e/mock-host/inventoryModel.ts` など）は仕様の読み物として参照する |
| 完了の定義 | §6.1 の 15 項目。加えて、RemoteExec の再ホスト、DSL の置き換え、NuGet の整理、`WebUiInputExclusivity` の改名、`WebPinId` などの改名、旧 UITK SkitUI の残骸の処理 |
| AI 向けの作法 | 表示用に確定させた値を C# の型付き DTO（判別 union は sealed 継承）で持たせると、UXML 側が薄く保てる。§5.1 の分割は Topic 1 本ごとに独立した PR にできる |
| 第一弾の画面 | インベントリには grab、D&D、ツールチップ、i18n がそろっているが、InventoryPanel が 3 画面で共有されるので、単独で移すと Web パネルとの同居が必ず起きる。ホットバーはビルドメニューからの D&D と組でなければ移せない。**入力を持たない HUD**（crosshair、progress、placement mode、key hint、challenge HUD、train riding）を先に移す案もある。地ならし（描画順の実測、PanelSettings、stage と viewport のスケール、名前付き補間、Hub と無関係な DI）を入力の取り合い無しに検証できる。どちらを選ぶかはユーザーの裁定 |

---

## 9. 推測と未検証の一覧

| 項目 | 状態 |
|---|---|
| シーンに EventSystem があるとき、UITK パネル上で `EventSystem.IsPointerOverGameObject()` が true を返すか | 推測。Unity 6000.3.8f1 で実測が要る |
| uGUI の Screen Space Overlay Canvas（CEF の RawImage）と UITK パネルの描画順の決まり方 | 推測。実測が要る |
| CEF と UITK の TextField が `Input.imeCompositionMode` と `compositionString` を取り合うか | 推測。実機の IME で確認が要る |
| プレイテストの InputSystem 注入が EventSystem 経由で UITK に届くか | 推測 |
| `Microsoft.Extensions.*` を AspNetCore 以外（ModelContextProtocol など）が共有しているか | 未確認。NuGet を整理する前に依存グラフを確認する |
| `BlockInventoryTopic` のコメントは「4tick」だが、実装は `Observable.IntervalFrame(4)`（フレーム単位）（`BlockInventoryTopic.cs:77-80`） | 事実（コメントと実装の食い違い。移植するときに意図を確かめる） |
| `tutorial.anchor_ack` に副作用が無い（照合して成否を返すだけ） | 事実（`TutorialAnchorAckAction.cs`）。ack の結果を誰が使うかは Web 側の再送制御だけ。UITK で同じ仕組みが要るかは要検討 |
