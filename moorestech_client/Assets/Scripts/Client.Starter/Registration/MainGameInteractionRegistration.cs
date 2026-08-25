using System;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Control;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Player.StateController.State;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Train.DebugView;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UnlockState;
using Client.Skit.Context;
using Client.Skit.Skit;
using Game.PlacementTarget;
using Game.PlayerRiding.Interface;
using Game.UnlockState;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    internal static class MainGameInteractionRegistration
    {
        public static void Register(ContainerBuilder builder, Client.Network.API.InitialHandshakeResponse handshake)
        {
            RegisterPlacement(builder);
            RegisterUiAndPlayer(builder);

            // skit基準を登録
            // Register the shared skit context and spawn-relative origin
            var skitActionContext = new SkitActionContext();
            builder.RegisterInstance<ISkitActionContext>(skitActionContext);
            builder.RegisterInstance<ISkitActionController>(skitActionContext);
            builder.RegisterInstance(new SkitOrigin(handshake.MapLayout.Spawn));

            RegisterRuntimeServices(builder);
        }

        private static void RegisterPlacement(ContainerBuilder builder)
        {
            builder.Register<CommonBlockPlaceSystem>(Lifetime.Singleton);
            builder.Register<BeltConveyorPlaceSystem>(Lifetime.Singleton);
            builder.Register<ITrainCarPlacementDetector, TrainCarPlacementDetector>(Lifetime.Singleton);
            builder.Register<TrainCarPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailPlaceSystem>(Lifetime.Singleton);
            builder.Register<TrainRailConnectSystem>(Lifetime.Singleton);
            builder.Register<GearChainPoleConnectSystem>(Lifetime.Singleton);
            builder.Register<ElectricWireConnectSystem>(Lifetime.Singleton);
            builder.Register<IPlacementFeedbackPresenter, PlacementFeedbackTooltipPresenter>(Lifetime.Singleton);
            builder.Register<PlaceSystemStateController>(Lifetime.Singleton);
            builder.Register<IPlaceSystemSelector, PlaceSystemSelector>(Lifetime.Singleton);
            builder.Register<ClientBlueprintLibrary>(Lifetime.Singleton);
            builder.Register<MapVeinRangeViewService>(Lifetime.Singleton).As<IMapVeinRangeView>();
            builder.Register<PlacementTargetCatalog>(Lifetime.Singleton);
            builder.Register<BlueprintPasteSystem>(Lifetime.Singleton);
            builder.Register<BlueprintCopySystem>(Lifetime.Singleton);
            builder.Register<PlacementTargetResolver>(Lifetime.Singleton);
            builder.Register<HotbarKeyInput>(Lifetime.Singleton);
            builder.Register<HotbarTapInputService>(Lifetime.Singleton);
            builder.Register<HotbarSelectionReconciler>(Lifetime.Singleton);
        }

        private static void RegisterUiAndPlayer(ContainerBuilder builder)
        {
            builder.Register<IPlayerViewApplier, PlayerViewApplier>(Lifetime.Singleton);
            builder.Register<IPlayerCameraInteractionApplier, PlayerCameraInteractionApplier>(Lifetime.Singleton);
            builder.Register<PlayerViewModeController>(Lifetime.Singleton).AsSelf().As<IStartable>().As<ITickable>();
            builder.Register<UiStateCameraPolicyService>(Lifetime.Singleton);

            // UI state群を単一の辞書へ集約する
            // Gather UI states into their single dictionary
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

            builder.Register<NormalPlayerState>(Lifetime.Singleton);
            builder.Register<TrainCarRideFollowTargetResolver>(Lifetime.Singleton).As<IRideFollowTargetResolver>();
            builder.Register<RidingPlayerState>(Lifetime.Singleton);
            builder.Register<PlayerStateDictionary>(Lifetime.Singleton);
            builder.Register<PlayerStateController>(Lifetime.Singleton).AsSelf().As<ITickable>();
        }

        private static void RegisterRuntimeServices(ContainerBuilder builder)
        {
            builder.Register<TutorialManager>(Lifetime.Singleton);
            builder.Register<IGameUnlockStateData, ClientGameUnlockStateData>(Lifetime.Singleton);
            builder.Register<RailGraphClientCache>(Lifetime.Singleton);
            builder.Register<ClientStationReferenceRegistry>(Lifetime.Singleton)
                .AsSelf().As<IInitializable>().As<IDisposable>();
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
