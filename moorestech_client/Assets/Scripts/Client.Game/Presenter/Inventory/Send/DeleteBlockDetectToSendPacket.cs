using Client.Game.Control.MouseKeyboard;
using Client.Game.UI.UIState;
using MainGame.Network.Send;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class DeleteBlockDetectToSendPacket : ITickable
    {
        private readonly UIStateControl _uiStateControl;

        public DeleteBlockDetectToSendPacket(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public void Tick()
        {
            if (_uiStateControl.CurrentState == UIStateEnum.DeleteBar &&
                BlockClickDetect.TryGetClickBlockPosition(out var position))
            {
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.DestroyBlock);
            }
        }
    }
}