using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public interface IBlockClickDetect
    {
        public bool IsBlockClicked();
        public Vector2Int GetClickPosition();
        public GameObject GetClickedObject();
    }
}