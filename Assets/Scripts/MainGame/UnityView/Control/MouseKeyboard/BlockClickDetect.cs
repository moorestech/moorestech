using System;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block;
using UnityEngine;
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

        public bool TryGetPosition(out Vector2Int position)
        {
            position = Vector2Int.zero;

            if (!TryGetBlock(out var blockObject)) return false;
            
            
            var blockPos = blockObject.transform.position;
            position = new Vector2Int((int) blockPos.x, (int) blockPos.z);
                
            return true;
        }
        
        
        public bool TryGetBlock(out GameObject blockObject)
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