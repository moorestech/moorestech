using Client.Game.InGame.UI.UIState;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// UI の状態の変化を「失敗率の改善方法」のモーダルへ渡し、機械の画面と一緒に閉じさせる
    /// Passes UI state changes to the improvement modal so it closes together with the machine screen
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalCloser : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly CleanRoomYieldImprovementModalBridge _bridge;

        public CleanRoomYieldImprovementModalCloser(UIStateControl uiStateControl, CleanRoomYieldImprovementModalBridge bridge)
        {
            _uiStateControl = uiStateControl;
            _bridge = bridge;
        }

        public void Initialize()
        {
            _uiStateControl.OnStateChanged.Subscribe(_bridge.CloseUnlessMachineScreen);
        }
    }
}
