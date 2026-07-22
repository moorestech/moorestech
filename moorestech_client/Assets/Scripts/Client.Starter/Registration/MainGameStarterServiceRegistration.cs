using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Player.StateController.State;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.InGame.UnlockState;
using Client.Game.InGame.World;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.DebugView;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Skit.Skit;
using Core.Item.Interface;
using Game.Context;
using Game.UnlockState;
using VContainer;
using VContainer.Unity;

namespace Client.Starter
{
    /// <summary>
    ///     MainGameStarterのPureC#系DI登録をまとめたヘルパ
    ///     Helper that groups pure C# DI registrations for MainGameStarter
    /// </summary>
    public static class MainGameStarterServiceRegistration
    {
        // インベントリのUIコントロールとスタックレベル解放を登録
        // Register inventory UI control and stack level unlock
        public static void RegisterInventory(IContainerBuilder builder)
        {
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory, LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();
            // スタックレベルの変更系はDI注入のみで公開する
            // Expose stack level mutation only through DI injection
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelUnlocker>());
            builder.RegisterEntryPoint<ItemStackLevelEventHandler>();
        }

        // プレゼンターアセンブリとネットワークハンドラを登録
        // Register presenter assembly and network handlers
        public static void RegisterNetworkHandlers(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<CommonMachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<WorldDataHandler>();
            // コライダーの距離カリング（汎用マネージャ＋ブロック登録サービス）
            // Collider distance culling (generic manager + block register service)
            builder.Register<ColliderDistanceCullingManager>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.RegisterEntryPoint<BlockColliderCullingRegisterService>();
            builder.RegisterEntryPoint<PlayerPositionSender>();
            builder.RegisterEntryPoint<SkitFireManager>();
            builder.RegisterEntryPoint<RailGraphCacheNetworkHandler>();
            builder.RegisterEntryPoint<RailGraphConnectionNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitSnapshotEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitTickDiffBundleEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainFullSnapshotEventNetworkHandler>().AsSelf();
        }

        // 設置システムを登録
        // Register placement systems
        public static void RegisterPlacementSystems(IContainerBuilder builder)
        {
            builder.Register<CommonBlockPlaceSystem>(Lifetime.Singleton);
            builder.Register<BeltConveyorPlaceSystem>(Lifetime.Singleton);
            builder.Register<ITrainCarPlacementDetector, TrainCarPlacementDetector>(Lifetime.Singleton);
            builder.Register<TrainCarPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailConnectSystem>(Lifetime.Singleton);
            builder.Register<GearChainPoleConnectSystem>(Lifetime.Singleton);
            builder.Register<ElectricWireConnectSystem>(Lifetime.Singleton);
            builder.Register<PlaceSystemStateController>(Lifetime.Singleton);
            builder.Register<PlaceSystemSelector>(Lifetime.Singleton);
            builder.Register<ClientBlueprintLibrary>(Lifetime.Singleton);
            builder.Register<BlueprintPasteSystem>(Lifetime.Singleton);
            builder.Register<BlueprintCopySystem>(Lifetime.Singleton);
        }

        // UI非依存の視点モード処理を登録
        // Register UI-independent view-mode processing
        public static void RegisterViewMode(IContainerBuilder builder)
        {
            builder.Register<IPlayerViewApplier, PlayerViewApplier>(Lifetime.Singleton);
            builder.Register<IPlayerCameraInteractionApplier, PlayerCameraInteractionApplier>(Lifetime.Singleton);
            builder.Register<PlayerViewModeController>(Lifetime.Singleton).AsSelf().As<IStartable>().As<ITickable>();
        }

        // UIステート群を登録
        // Register UI state group
        public static void RegisterUiStates(IContainerBuilder builder)
        {
            builder.Register<UIStateDictionary>(Lifetime.Singleton);
            builder.Register<SubInventoryState>(Lifetime.Singleton);
            builder.Register<GameScreenState>(Lifetime.Singleton);
            builder.Register<PauseMenuState>(Lifetime.Singleton);
            builder.Register<PlayerInventoryState>(Lifetime.Singleton);
            builder.Register<DeleteObjectState>(Lifetime.Singleton);
            builder.Register<SkitState>(Lifetime.Singleton);
            builder.Register<PlaceBlockState>(Lifetime.Singleton);
            builder.Register<ChallengeListState>(Lifetime.Singleton);
            builder.Register<ResearchTreeState>(Lifetime.Singleton);
            builder.Register<DebugBlockInfoState>(Lifetime.Singleton);
            builder.Register<TrainHUDScreenState>(Lifetime.Singleton);
            builder.Register<BuildMenuState>(Lifetime.Singleton);
            builder.Register<BuildOperationHistory>(Lifetime.Singleton);
            builder.Register<BuildUndoService>(Lifetime.Singleton);
            builder.Register<ItemRecipeViewerDataContainer>(Lifetime.Singleton);
            builder.Register<GameScreenSubInventoryInteractService>(Lifetime.Singleton);
            builder.Register<PlacementTargetPickService>(Lifetime.Singleton);
            builder.Register<RideVehicleInputService>(Lifetime.Singleton);
            builder.Register<PauseMenuStateService>(Lifetime.Singleton);
        }

        // プレイヤーステート（UIState → PlayerStateController の単方向依存）を登録
        // Register player state framework (one-way dependency: UIState → PlayerStateController)
        public static void RegisterPlayerStates(IContainerBuilder builder)
        {
            builder.Register<NormalPlayerState>(Lifetime.Singleton);
            builder.Register<TrainCarRideFollowTargetResolver>(Lifetime.Singleton).As<IRideFollowTargetResolver>();
            builder.Register<RidingPlayerState>(Lifetime.Singleton);
            builder.Register<PlayerStateDictionary>(Lifetime.Singleton);
            builder.Register<PlayerStateController>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        // スキット関連のコンテキストを登録
        // Register skit related context
        public static void RegisterSkit(IContainerBuilder builder)
        {
            var skitActionContext = new SkitActionContext();
            builder.RegisterInstance<ISkitActionContext>(skitActionContext);
            builder.RegisterInstance<ISkitActionController>(skitActionContext);
        }

        // その他のクライアントサービス（チュートリアル・列車キャッシュ等）を登録
        // Register other client services (tutorial, train caches, etc.)
        public static void RegisterClientServices(IContainerBuilder builder)
        {
            builder.Register<TutorialManager>(Lifetime.Singleton);
            builder.Register<IGameUnlockStateData, ClientGameUnlockStateData>(Lifetime.Singleton);
            builder.Register<RailGraphClientCache>(Lifetime.Singleton);
            builder.Register<ClientStationReferenceRegistry>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<IDisposable>();
            builder.Register<RailGraphSnapshotApplier>(Lifetime.Singleton);
            builder.Register<TrainUnitClientCache>(Lifetime.Singleton);
            builder.Register<TrainUnitTickState>(Lifetime.Singleton);
            builder.Register<TrainUnitFutureMessageBuffer>(Lifetime.Singleton);
            builder.Register<TrainUnitSnapshotApplier>(Lifetime.Singleton);
            builder.Register<TrainUnitVisualUpdateSystem>(Lifetime.Singleton);
            builder.Register<TrainUnitClientSimulator>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.Register<TrainUnitHashVerifier>(Lifetime.Singleton).As<ITrainUnitHashTickGate>().As<IDisposable>();
            builder.Register<TrainUnitDebugOverlayPresenter>(Lifetime.Singleton).As<ITickable>().As<IDisposable>();
        }
    }
}
