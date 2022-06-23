using UnityEngine;

namespace MainGame.UnityView.Game
{
    public interface IPlayerPosition
    {
        public Vector2 GetPlayerPosition();
        public void SetPlayerPosition(Vector2 vector2);
        public void SetActive(bool active);
    }
}