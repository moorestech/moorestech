using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.WorldMapTile;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    public class OreMapTileClickDetect : MonoBehaviour
    {
        private Camera _mainCamera;
        private SendMiningProtocol _sendMiningProtocol;
        private UIStateControl _uiStateControl; 
        
        [Inject]
        public void Construct(Camera mainCamera,SendMiningProtocol sendMiningProtocol,UIStateControl uiStateControl)
        {
            _mainCamera = mainCamera;
            _sendMiningProtocol = sendMiningProtocol;
            _uiStateControl = uiStateControl;
            
        }

        private void Update()
        {
            if (IsBlockClicked() && _uiStateControl.CurrentState == UIStateEnum.DeleteBar)
            {
                _sendMiningProtocol.Send(GetClickPosition());
            }
        }

        private bool IsBlockClicked()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置にタイルマップがあるとき
            if (!InputManager.Playable.ScreenClick.GetKey) return false;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (!Physics.Raycast(ray, out var hit)) return false;
            if (hit.collider.gameObject.GetComponent<OreTileObject>() == null) return false;

            return true;
        }

        private Vector2Int GetClickPosition()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
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
    }
}