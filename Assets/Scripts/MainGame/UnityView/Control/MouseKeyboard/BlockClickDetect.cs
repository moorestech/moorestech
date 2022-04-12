using System;
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
            if (hit.collider.gameObject.GetComponent<BlockGameObject>() == null) return false;

            return true;
        }

        public Vector2Int GetClickPosition()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.Raycast(ray, out var hit))
            {            
                var x = Mathf.RoundToInt(hit.point.x);
                var y = Mathf.RoundToInt(hit.point.z);
                return new Vector2Int(x, y);
            }
            else
            {
                return Vector2Int.zero;
            }
        }

        public GameObject GetClickedObject()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject.GetComponent<BlockGameObject>())
            {
                return hit.collider.gameObject;
            }
            throw new Exception("クリックしたオブジェクトが見つかりませんでした");
        }
    }
}