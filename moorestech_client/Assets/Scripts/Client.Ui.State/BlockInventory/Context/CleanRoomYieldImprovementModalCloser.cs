using Client.Game.InGame.UI.UIState;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 機械の画面と一緒に改善方法モーダルを閉じる
    /// Closes the improvement modal together with the machine screen
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
