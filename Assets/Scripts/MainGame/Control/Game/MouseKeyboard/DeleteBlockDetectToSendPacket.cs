using MainGame.Control.UI.UIState;
using MainGame.Network.Send;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Control.Game.MouseKeyboard
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
                if (_blockClickDetect.IsBlockClicked())
                {
                    var pos = _blockClickDetect.GetClickPosition();
                    _sendBlockRemoveProtocol.Send(pos.x,pos.y);
                }
            }   
        }
    }
}