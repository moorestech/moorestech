using System;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class BlockClickDetect : MonoBehaviour,IBlockClickDetect
    {
        private Camera _mainCamera;
        
        [Inject]
        public void Construct(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        public bool TryGetCursorOnBlockPosition(out Vector2Int position)
        {
            position = Vector2Int.zero;

            if (!TryGetCursorOnBlock(out var blockObject)) return false;
            
            
            position = blockObject.BlockPosition;
                
            return true;
        }

        public bool TryGetClickBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (InputManager.Settings.Playable.ScreenClick.triggered && TryGetCursorOnBlock(out blockObject))
            {
                return true;
            }

            blockObject = null;
            return false;
        }

        public bool TryGetClickBlockPosition(out Vector2Int position)
        {
            if (InputManager.Settings.Playable.ScreenClick.triggered && TryGetCursorOnBlockPosition(out position))
            {
                return true;
            }

            position = Vector2Int.zero;
            return false;
        }
        
        
        

        private bool TryGetCursorOnBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            
            var mousePosition = InputManager.Settings.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            if (!Physics.Raycast(ray, out var hit)) return false;
            var child = hit.collider.gameObject.GetComponent<BlockGameObjectChild>();
            if (child is null) return false;

            
            blockObject = child.BlockGameObject;
            
            return true;
        }
    }
}