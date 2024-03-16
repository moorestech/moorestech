using MainGame.ModLoader.Glb;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public interface IBlockClickDetect
    {
        public bool TryGetCursorOnBlockPosition(out Vector3Int position);
        public bool TryGetClickBlock(out BlockGameObject blockObject);
        public bool TryGetClickBlockPosition(out Vector3Int position);
    }
}