using MainGame.Network.Send.SocketUtil;
using UnityEngine;

namespace MainGame.Control.Game
{
    public class PlayerPosition : MonoBehaviour,IPlayerPosition
    {
        public Vector2 GetPlayerPosition()
        {
            var position = transform.position;
            return new Vector2(position.x, position.z);
        }
    }
}