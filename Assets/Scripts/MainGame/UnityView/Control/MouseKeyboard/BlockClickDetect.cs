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
        private MoorestechInputSettings _input;
        
        [Inject]
        public void Construct(Camera mainCamera)
        {
            _mainCamera = mainCamera;
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        public bool TryGetCursorOnBlockPosition(out Vector2Int position)
        {
            position = Vector2Int.zero;

            if (!TryGetCursorOnBlock(out var blockObject)) return false;
            
            
            var blockPos = blockObject.transform.position;
            position = new Vector2Int((int) blockPos.x, (int) blockPos.z);
                
            return true;
        }

        public bool TryGetClickBlock(out GameObject blockObject)
        {
            blockObject = null;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (_input.Playable.ScreenClick.triggered && TryGetCursorOnBlock(out blockObject))
            {
                return true;
            }

            blockObject = null;
            return false;
        }

        public bool TryGetClickBlockPosition(out Vector2Int position)
        {
            if (_input.Playable.ScreenClick.triggered && TryGetCursorOnBlockPosition(out position))
            {
                return true;
            }

            position = Vector2Int.zero;
            return false;
        }
        
        
        

        private bool TryGetCursorOnBlock(out GameObject blockObject)
        {
            blockObject = null;
            
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            if (!Physics.Raycast(ray, out var hit)) return false;
            var child = hit.collider.gameObject.GetComponent<BlockGameObjectChild>();
            if (child is null) return false;

            
            blockObject = child.BlockGameObject.gameObject;
            
            return true;
        }
    }
}