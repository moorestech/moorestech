using UnityEngine;

namespace MainGame.Network.Interface.Send
{
    public interface ISendPlayerPosition
    {
        public void Send(int playerId,Vector2 position);
    }
}