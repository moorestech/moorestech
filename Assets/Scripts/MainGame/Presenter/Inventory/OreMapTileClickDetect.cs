using MainGame.Network.Send;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.WorldMapTile;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    public class OreMapTileClickDetect : MonoBehaviour
    {
        private Camera _mainCamera;
        private MoorestechInputSettings _input;
        private SendMiningProtocol _sendMiningProtocol;
        private UIStateControl _uiStateControl; 
        
        [Inject]
        public void Construct(Camera mainCamera,SendMiningProtocol sendMiningProtocol,UIStateControl uiStateControl)
        {
            _mainCamera = mainCamera;
            _sendMiningProtocol = sendMiningProtocol;
            _uiStateControl = uiStateControl;
            
            _input = new MoorestechInputSettings();
            _input.Enable();
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
             var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置にタイルマップがあるとき
            if (!_input.Playable.ScreenClick.triggered) return false;
            if (!Physics.Raycast(ray, out var hit)) return false;
            if (hit.collider.gameObject.GetComponent<OreTileObject>() == null) return false;
            
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
    }
}