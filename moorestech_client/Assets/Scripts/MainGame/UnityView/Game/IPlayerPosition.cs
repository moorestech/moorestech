using UnityEngine;

namespace MainGame.UnityView.Game
{
    public interface IPlayerPosition
    {
        public Vector2 GetPlayerPosition();
        public void SetPlayerPosition(Vector2 playerPos);
        public void SetActive(bool active);
    }
}