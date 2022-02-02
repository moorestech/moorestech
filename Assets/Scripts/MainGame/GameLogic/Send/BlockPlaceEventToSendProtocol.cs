using MainGame.Network.Interface.Send;
using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;

namespace MainGame.GameLogic.Send
{
    public class BlockPlaceEventToSendProtocol
    {
        private readonly ISendPlaceHotBarBlockProtocol _sendPlaceHotBarBlockProtocol;
        private readonly int _playerId;

        public BlockPlaceEventToSendProtocol(
            ISendPlaceHotBarBlockProtocol sendPlaceHotBarBlockProtocol,
            IBlockPlaceEvent blockPlaceEvent,
            ConnectionPlayerSetting connectionPlayerSetting)
        {
            _sendPlaceHotBarBlockProtocol = sendPlaceHotBarBlockProtocol;
            blockPlaceEvent.Subscribe(OnBlockPlace);
            _playerId = connectionPlayerSetting.PlayerId;
        }

        public void OnBlockPlace(Vector2Int position, short hotBar)
        {
            _sendPlaceHotBarBlockProtocol.Send(position.x,position.y,hotBar,_playerId);
        }
    }
}