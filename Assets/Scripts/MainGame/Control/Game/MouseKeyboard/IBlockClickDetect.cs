using UnityEngine;

namespace MainGame.Control.Game.MouseKeyboard
{
    public interface IBlockClickDetect
    {
        public bool IsBlockClicked();
        public Vector2Int GetClickPosition();
    }
}