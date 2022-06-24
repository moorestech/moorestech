using MainGame.Network.Send;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class DeleteBlockDetectToSendPacket : ITickable
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly SendBlockRemoveProtocol _sendBlockRemoveProtocol;
        private readonly UIStateControl _uiStateControl;

        public DeleteBlockDetectToSendPacket(IBlockClickDetect blockClickDetect, SendBlockRemoveProtocol sendBlockRemoveProtocol, UIStateControl uiStateControl)
        {
            _blockClickDetect = blockClickDetect;
            _sendBlockRemoveProtocol = sendBlockRemoveProtocol;
            _uiStateControl = uiStateControl;
        }

        public void Tick()
        {
            if (_uiStateControl.CurrentState == UIStateEnum.DeleteBar)
            {
                if (_blockClickDetect.TryGetPosition(out var position))
                {
                    _sendBlockRemoveProtocol.Send(position.x,position.y);
                }
            }   
        }
    }
}