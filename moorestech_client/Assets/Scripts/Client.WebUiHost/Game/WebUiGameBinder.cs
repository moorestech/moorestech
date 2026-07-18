using Client.Game.InGame.Context;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Game.UnlockState;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Electric;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.ContextMenu;
using VContainer;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// ゲーム系トピックを WebUiHost.Hub にバインドする facade。
    /// ClientContext / ClientDIContext の準備が終わった後に 1 回呼ぶ。
    /// Facade that binds game-side topics to WebUiHost.Hub.
    /// Must be called once, after ClientContext / ClientDIContext are ready.
    /// </summary>
    public static class WebUiGameBinder
    {
        public static void Bind()
        {
            var hub = Boot.WebUiHost.Hub;
            if (hub == null)
            {
                // WebUiHost が起動失敗していると topic/action が一切登録されないため痕跡を残す
                // Leave a trace when WebUiHost failed to start, since no topics/actions get bound
                UnityEngine.Debug.LogWarning("[WebUiHost] Bind skipped: hub is null (WebUiHost not started)");
                return;
            }

            // DI からインベントリコントローラと UI 系コンポーネントを取得
            // Resolve the inventory controller and UI components from DI
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var controller = resolver.Resolve<LocalPlayerInventoryController>();
            var hotBarView = resolver.Resolve<HotBarView>();
            var uiStateControl = resolver.Resolve<UIStateControl>();
            var subInventoryState = resolver.Resolve<SubInventoryState>();

            // インベントリトピックを生成して Hub に登録（selectedHotbar 用に HotBarView を渡す）
            // Create inventory topic and register it (HotBarView is passed for selectedHotbar)
            var inventoryTopic = new InventoryTopic(hub, controller, hotBarView);
            hub.RegisterTopic(InventoryTopic.TopicName, inventoryTopic);

            // モーダルブリッジサービスを生成（topic と action で共有）
            // Create the modal bridge service (shared by topic and action)
            var modalService = new WebUiModalService();

            // モーダルトピックを登録
            // Register the modal topic
            var modalTopic = new ModalTopic(hub, modalService);
            hub.RegisterTopic(ModalTopic.TopicName, modalTopic);

            // 進捗バートピックを登録（ProgressBarView は静的シングルトン）
            // Register the progress-bar topic (ProgressBarView is a static singleton)
            var progressTopic = new ProgressTopic(hub, ProgressBarView.Instance);
            hub.RegisterTopic(ProgressTopic.TopicName, progressTopic);

            // ブロックインベントリトピックを登録
            // Register the block-inventory topic
            var blockInventoryTopic = new BlockInventoryTopic(hub, uiStateControl, subInventoryState);
            hub.RegisterTopic(BlockInventoryTopic.TopicName, blockInventoryTopic);

            // UIステートトピックを登録（Web側画面ルーティングの正）
            // Register the UI-state topic (source of truth for web-side routing)
            var uiStateTopic = new UiStateTopic(hub, uiStateControl);
            hub.RegisterTopic(UiStateTopic.TopicName, uiStateTopic);

            // 現在言語トピックを登録（辞書本体はHTTP endpointから取得）
            // Register the current-locale topic (dictionary bodies come from the HTTP endpoint)
            var localizationTopic = new LocalizationTopic(hub);
            hub.RegisterTopic(LocalizationTopic.TopicName, localizationTopic);

            // ポーズメニューの切断表示を登録する
            // Register the pause-menu disconnect presentation
            var networkDisconnectPresenter = resolver.Resolve<NetworkDisconnectPresenter>();
            var pauseMenuTopic = new PauseMenuTopic(hub, networkDisconnectPresenter);
            hub.RegisterTopic(PauseMenuTopic.TopicName, pauseMenuTopic);

            // 設置モードHUDを既存の設置状態と給電範囲へ接続する
            // Connect the placement HUD to the existing placement state and energized range
            var placementModeTopic = new PlacementModeTopic(hub, resolver.Resolve<PlaceSystemStateController>(),
                resolver.Resolve<PlaceBlockState>(), resolver.Resolve<DisplayEnergizedRange>());
            hub.RegisterTopic(PlacementModeTopic.TopicName, placementModeTopic);

            // 削除可否理由を削除HUDへ配信する
            // Publish delete denial reasons to the delete HUD
            var deleteModeTopic = new DeleteModeTopic(hub, resolver.Resolve<DeleteObjectState>());
            hub.RegisterTopic(DeleteModeTopic.TopicName, deleteModeTopic);

            // 状態外の共通HUDを各既存ビューの変更通知へ接続する
            // Connect state-independent HUD topics to the existing view notifications
            hub.RegisterTopic(KeyHintsTopic.TopicName, new KeyHintsTopic(hub, KeyControlDescription.Instance));
            hub.RegisterTopic(CrosshairTopic.TopicName, new CrosshairTopic(hub, CrosshairView.Instance));
            hub.RegisterTopic(UiVisibilityTopic.TopicName, new UiVisibilityTopic(hub, UIRoot.Instance));

            // 直接採掘HUDを固定間隔サンプリングTopicへ接続する
            // Connect direct-mining HUD state to a fixed-interval sampled topic
            hub.RegisterTopic(MiningHudTopic.TopicName, new MiningHudTopic(hub, resolver.Resolve<MapObjectMiningController>()));

            // uGUI/3D由来のツールチップを共通Web基盤へ接続する
            // Connect uGUI/3D tooltip sources to the shared web tooltip foundation
            hub.RegisterTopic(TooltipTopic.TopicName, new TooltipTopic(hub, MouseCursorTooltip.Instance));

            // コンテキストメニュー項目と既存callbackをTopic/Actionへ接続する
            // Connect context-menu items and existing callbacks through Topic/Action
            var contextMenuView = ContextMenuView.Instance;
            hub.RegisterTopic(ContextMenuTopic.TopicName, new ContextMenuTopic(hub, contextMenuView));
            hub.RegisterAction(new ContextMenuSelectActionHandler(contextMenuView));
            hub.RegisterAction(new ContextMenuCloseActionHandler(contextMenuView));

            // クラフトレシピトピックを登録
            // Register the craft-recipes topic
            var unlockStateData = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<IGameUnlockStateData>();
            var craftingRecipesTopic = new CraftingRecipesTopic(hub, unlockStateData);
            hub.RegisterTopic(CraftingRecipesTopic.TopicName, craftingRecipesTopic);

            // 機械レシピトピックを登録
            // Register the machine-recipes topic
            var machineRecipesTopic = new MachineRecipesTopic(hub, unlockStateData);
            hub.RegisterTopic(MachineRecipesTopic.TopicName, machineRecipesTopic);

            // アイテムリストトピックを登録
            // Register the item-list topic
            var recipeContainer = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<ItemRecipeViewerDataContainer>();
            var itemListTopic = new RecipeViewerItemListTopic(hub, recipeContainer);
            hub.RegisterTopic(RecipeViewerItemListTopic.TopicName, itemListTopic);

            // 研究ツリートピックを登録（表示可否は ui_state.current 側で判定）
            // Register the research-tree topic (visibility is decided by ui_state.current)
            var researchTopic = new ResearchTopic(hub, uiStateControl);
            hub.RegisterTopic(ResearchTopic.TopicName, researchTopic);

            // ビルドメニュートピックを登録（BP名入力ブリッジも同時に張る）
            // Register the build-menu topic (also wires the blueprint-name input bridge)
            var blueprintLibrary = resolver.Resolve<ClientBlueprintLibrary>();
            var buildMenuView = resolver.Resolve<BuildMenuView>();
            var blueprintNameInputView = resolver.Resolve<BlueprintNameInputView>();
            var buildMenuTopic = new BuildMenuTopic(hub, uiStateControl, unlockStateData, blueprintLibrary);
            hub.RegisterTopic(BuildMenuTopic.TopicName, buildMenuTopic);
            new BlueprintNameInputWebBridge(blueprintNameInputView, modalService);

            // action ハンドラ登録
            // Register action handlers
            // debug.echo は EchoActionHandler と同じくエディタ/開発ビルド限定で登録する
            // Register debug.echo only in editor/development builds, matching EchoActionHandler's gate
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            hub.RegisterAction(new EchoActionHandler());
#endif
            hub.RegisterAction(new MoveItemActionHandler(controller));
            hub.RegisterAction(new SplitGrabActionHandler(controller));
            hub.RegisterAction(new CollectActionHandler(controller));
            hub.RegisterAction(new SortInventoryActionHandler(controller));
            hub.RegisterAction(new CraftExecuteActionHandler(unlockStateData));
            hub.RegisterAction(new SelectHotbarActionHandler(hotBarView));
            hub.RegisterAction(new ModalRespondActionHandler(modalService));
            hub.RegisterAction(new BlockMoveItemActionHandler(controller, subInventoryState));
            hub.RegisterAction(new BlockSplitGrabActionHandler(controller, subInventoryState));
            hub.RegisterAction(new BlockCollectActionHandler(controller, subInventoryState));
            hub.RegisterAction(new RequestUiStateActionHandler(uiStateControl));
            hub.RegisterAction(new ResearchCompleteActionHandler(researchTopic));
            hub.RegisterAction(new FilterSplitterSetModeActionHandler(subInventoryState, blockInventoryTopic));
            hub.RegisterAction(new FilterSplitterSetFilterItemActionHandler(subInventoryState, controller, blockInventoryTopic));
            hub.RegisterAction(new BuildMenuSelectActionHandler(uiStateControl, unlockStateData, blueprintLibrary, buildMenuView));
            hub.RegisterAction(new BlueprintDeleteActionHandler(blueprintLibrary));
            hub.RegisterAction(new PauseMenuSaveActionHandler(resolver.Resolve<SaveButton>()));
            hub.RegisterAction(new PauseMenuBackToMainMenuActionHandler(resolver.Resolve<BackToMainMenu>()));
        }
    }
}
