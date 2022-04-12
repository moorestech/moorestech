using UnityEngine;

namespace MainGame.UnityView.Game
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