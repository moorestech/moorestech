using MainGame.UnityView.Chunk;
using MainGame.UnityView.ControllerInput;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace MainGame.Control.Game.MouseKeyboard
{
    /// <summary>
    /// マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class MouseGroundClickInput : MonoBehaviour,IControllerInput
    {
        private Camera _mainCamera;
        private GroundPlane _groundPlane;
        private MoorestechInputSettings _input;
        
        [Inject]
        public void Construct(Camera mainCamera,GroundPlane groundPlane)
        {
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
            _input = new MoorestechInputSettings();
            _input.Enable();
            _input.Playable.ScreenClick.performed += OnBlockPlace;
        }

        private void OnBlockPlace(InputAction.CallbackContext context)
        {
            
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            
            // マウスでクリックした位置が地面なら
            if (!Physics.Raycast(ray, out var hit)) return;
            if (hit.transform.gameObject != _groundPlane.gameObject)return;
            
            
            //イベントを発火
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);
            //TODO ホットバーのindexを取得する
            //TODO ブロックの設置をnetworkに伝える
        }

        public void OnInput()
        {
        }

        public void OffInput()
        {
            
        }
        
        
    }
}