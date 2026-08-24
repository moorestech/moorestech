using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.UIState;
using Client.Network.API;
using Client.Skit.UI;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using VContainer;

namespace Client.Starter.Registration
{
    internal static class MainGameContainerActivation
    {
        public static void ResolveRequiredServices(IObjectResolver resolver)
        {
            resolver.Resolve<BlockGameObjectDataStore>();
            resolver.Resolve<OutcropGameObjectDatastore>();
            resolver.Resolve<UIStateControl>();
            resolver.Resolve<EntityObjectDatastore>();
            resolver.Resolve<TrainCarObjectDatastore>();
            resolver.Resolve<ChallengeManager>();
            resolver.Resolve<PlayerSystemContainer>();
            resolver.Resolve<SkitUI>();
            resolver.Resolve<HotbarSelectionReconciler>();
        }

        public static void RestoreLoginState(IObjectResolver resolver, InitialHandshakeResponse response)
        {
            var context = new UITransitContext(UIStateEnum.GameScreen);
            var uiState = UIStateEnum.GameScreen;

            if (response.RidingTarget != null && response.RidingTarget.RidableType == RidableType.TrainCar)
            {
                var request = new InitialRideTrainCarRequest(
                    new TrainCarInstanceId(response.RidingTarget.TrainCarInstanceId), response.RidingSeatIndex);
                context = new UITransitContext(UIStateEnum.TrainHUDScreen, UITransitContextContainer.Create(request));
                uiState = UIStateEnum.TrainHUDScreen;
            }

            resolver.Resolve<UIStateControl>().Initialize(uiState, context);
        }
    }
}
