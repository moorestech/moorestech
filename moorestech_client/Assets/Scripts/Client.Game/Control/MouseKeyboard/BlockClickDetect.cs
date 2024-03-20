using Client.Game.Block;
using Constant;
using MainGame.UnityView.Control;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.Control.MouseKeyboard
{
    public static class BlockClickDetect
    {
        public static bool TryGetCursorOnBlockPosition(out Vector3Int position)
        {
            position = Vector3Int.zero;

            if (!TryGetCursorOnBlock(out var blockObject)) return false;


            position = blockObject.BlockPosition;

            return true;
        }

        public static bool TryGetClickBlockPosition(out Vector3Int position)
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && TryGetCursorOnBlockPosition(out position)) return true;

            position = Vector3Int.zero;
            return false;
        }

        public static bool TryGetClickBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && TryGetCursorOnBlock(out blockObject)) return true;

            blockObject = null;
            return false;
        }

        public static bool TryGetCursorOnBlock(out BlockGameObject blockObject)
        {
            blockObject = null;

            var ray = Camera.main.ScreenPointToRay(new Vector2(Screen.width / 2.0f, Screen.height / 2.0f));

            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.BlockOnlyLayerMask)) return false;
            var child = hit.collider.gameObject.GetComponent<BlockGameObjectChild>();
            if (child is null) return false;


            blockObject = child.BlockGameObject;

            return true;
        }
    }
}