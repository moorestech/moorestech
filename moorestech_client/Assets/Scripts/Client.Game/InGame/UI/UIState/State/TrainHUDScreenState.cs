using Client.Game.InGame.Control;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State
{
    // 列車に乗車中の HUD ステート。現状は GameScreen と同じ見た目を見せるだけだが、
    // 入力スコープは「降車のみ」に絞る（インベントリ等での乗車中断を避けるため）。
    // HUD state while riding a train; renders identically to GameScreen for now but
    // restricts input scope to dismount only (to avoid breaking the riding state by opening inventory etc.).
    public class TrainHUDScreenState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        private readonly RideVehicleInputService _rideVehicleInputService;
        private readonly TrainCarRidingState _trainCarRidingState;

        public TrainHUDScreenState(
            InGameCameraController inGameCameraController,
            RideVehicleInputService rideVehicleInputService,
            TrainCarRidingState trainCarRidingState)
        {
            _inGameCameraController = inGameCameraController;
            _rideVehicleInputService = rideVehicleInputService;
            _trainCarRidingState = trainCarRidingState;
        }

        public UITransitContext GetNextUpdate()
        {
            // サーバー強制降車・RPC失敗・降車成功のいずれでも IsRiding=false になり、GameScreen に戻る。
            // Server-forced dismount, RPC failure, and successful dismount all set IsRiding=false and return to GameScreen.
            if (!_trainCarRidingState.IsRiding) return new UITransitContext(UIStateEnum.GameScreen);

            // E で降車要求を送る（実遷移は次フレーム IsRiding=false 検知で行う）。
            // Send a dismount request on E; the actual transit happens on the next frame when IsRiding becomes false.
            _rideVehicleInputService.TryRequestDismount();

            // ポーズメニューだけは乗車中でも開ける（緊急退避用）。
            // Allow the pause menu while riding (escape hatch).
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);

            KeyControlDescription.Instance.SetText("E: 降車\nW/A/S/D: 列車操作\n");
        }

        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}
