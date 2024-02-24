using MainGame.Network.Send;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class DeleteBlockDetectToSendPacket : ITickable
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly UIStateControl _uiStateControl;

        public DeleteBlockDetectToSendPacket(IBlockClickDetect blockClickDetect, UIStateControl uiStateControl)
        {
            _blockClickDetect = blockClickDetect;
            _uiStateControl = uiStateControl;
        }

        public void Tick()
        {
            if (_uiStateControl.CurrentState == UIStateEnum.DeleteBar &&
                _blockClickDetect.TryGetClickBlockPosition(out var position))
            {
                SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.DestroyBlock);
            }
        }
    }
}