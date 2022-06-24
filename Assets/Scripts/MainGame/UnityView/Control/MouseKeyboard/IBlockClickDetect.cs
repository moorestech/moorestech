using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public interface IBlockClickDetect
    {
        public bool TryGetPosition(out Vector2Int position);
        public bool TryGetBlock(out GameObject blockObject);
    }
}