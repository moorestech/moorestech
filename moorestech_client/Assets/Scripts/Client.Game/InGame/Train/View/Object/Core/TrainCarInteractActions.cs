using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     Fで車両インベントリを開く
    ///     Opens the car inventory with F
    /// </summary>
    internal class TrainCarOpenInventoryInteractAction : ITapInteractAction
    {
        private readonly TrainCarEntityObject _trainCar;

        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory;
        public IReadOnlyList<string> HintParams => Array.Empty<string>();

        internal TrainCarOpenInventoryInteractAction(TrainCarEntityObject trainCar)
        {
            _trainCar = trainCar;
        }

        public InteractExecuteResult Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new TrainSubInventorySource(_trainCar.TrainCarInstanceId.AsPrimitive()));
            return InteractExecuteResult.Transit(new UITransitContext(UIStateEnum.SubInventory, container));
        }
    }

    /// <summary>
    ///     Eで乗車する
    ///     Boards the car with E
    /// </summary>
    internal class TrainCarRideInteractAction : ITapInteractAction
    {
        private readonly TrainCarEntityObject _trainCar;

        public InputKey Key => InputManager.Playable.Ride;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractRideTrain;
        public IReadOnlyList<string> HintParams => Array.Empty<string>();

        internal TrainCarRideInteractAction(TrainCarEntityObject trainCar)
        {
            _trainCar = trainCar;
        }

        public InteractExecuteResult Execute()
        {
            // TODO 他プレイヤーの乗車チェック（旧サービスから継承）
            // TODO check whether another player is already riding the train (inherited from RideVehicleInputService)
            var container = UITransitContextContainer.Create(new RideTrainCarRequest(_trainCar.TrainCarInstanceId));
            return InteractExecuteResult.Transit(new UITransitContext(UIStateEnum.TrainHUDScreen, container));
        }
    }
}
