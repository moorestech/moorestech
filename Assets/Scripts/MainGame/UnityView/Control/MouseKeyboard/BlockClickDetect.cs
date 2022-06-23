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
        
        public bool IsBlockClicked()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置が地面なら
            if (!_input.Playable.ScreenClick.triggered) return false;
            if (!Physics.Raycast(ray, out var hit)) return false;
            if (hit.collider.gameObject.GetComponent<BlockGameObjectChild>() == null) return false;

            return true;
        }

        public Vector2Int GetClickPosition()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (!_input.Playable.ScreenClick.triggered) return Vector2Int.zero;
            if (!Physics.Raycast(ray, out var hit)) return Vector2Int.zero;
            var child = hit.collider.gameObject.GetComponent<BlockGameObjectChild>();
            if (child == null) return Vector2Int.zero;
            
            
            var blockPos = child.BlockGameObject.transform.position;
            return new Vector2Int((int)blockPos.x,(int)blockPos.z);
        }

        public GameObject GetClickedObject()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject.GetComponent<BlockGameObjectChild>())
            {
                return hit.collider.gameObject.GetComponent<BlockGameObjectChild>().BlockGameObject.gameObject;
            }
            throw new Exception("クリックしたオブジェクトが見つかりませんでした");
        }
    }
}