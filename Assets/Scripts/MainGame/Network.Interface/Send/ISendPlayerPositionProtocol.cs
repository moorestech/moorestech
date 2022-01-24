using UnityEngine;

namespace MainGame.Network.Interface.Send
{
    public interface ISendPlayerPositionProtocol
    {
        public void Send(int playerId,Vector2 position);
    }
}