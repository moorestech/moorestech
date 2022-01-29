using MainGame.UnityView.Chunk;
using MainGame.UnityView.ControllerInput.Event;
using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.ControllerInput.MouseKeyboard
{
    /// <summary>
    /// マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class MouseGroundClickInput : MonoBehaviour,IControllerInput
    {
        private BlockPlaceEvent _blockPlaceEvent;
        private Camera _mainCamera;
        private GroundPlane _groundPlane;
        
        [Inject]
        public void Construct(IBlockPlaceEvent blockPlaceEvent,Camera mainCamera,GroundPlane groundPlane)
        {
            _blockPlaceEvent = blockPlaceEvent as BlockPlaceEvent;
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            
            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit)) return;
            // マウスでクリックした位置が地面なら
            if (hit.transform.gameObject != _groundPlane.gameObject)return;
            
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);
            //イベントを発火
            _blockPlaceEvent.OnOnBlockPlaceEvent(new Vector2Int(x,y),0);
        }

        public void OnInput()
        {
        }

        public void OffInput()
        {
            
        }
        
        
    }
}