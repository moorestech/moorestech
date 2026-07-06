using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Game.UnlockState;
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
            var itemListTopic = new RecipeViewerItemListTopic(hub, recipeContainer, unlockStateData);
            hub.RegisterTopic(RecipeViewerItemListTopic.TopicName, itemListTopic);

            // 研究ツリートピックを登録（表示可否は ui_state.current 側で判定）
            // Register the research-tree topic (visibility is decided by ui_state.current)
            var researchTopic = new ResearchTopic(hub, uiStateControl);
            hub.RegisterTopic(ResearchTopic.TopicName, researchTopic);

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
            hub.RegisterAction(new RequestUiStateActionHandler(uiStateControl));
            hub.RegisterAction(new ResearchCompleteActionHandler(researchTopic));
            hub.RegisterAction(new FilterSplitterSetModeActionHandler(subInventoryState, blockInventoryTopic));
            hub.RegisterAction(new FilterSplitterSetFilterItemActionHandler(subInventoryState, controller, blockInventoryTopic));
        }
    }
}
