using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public interface IBlockClickDetect
    {
        public bool TryGetCursorOnBlockPosition(out Vector2Int position);
        public bool TryGetClickBlock(out GameObject blockObject);
        public bool TryGetClickBlockPosition(out Vector2Int position);
    }
}