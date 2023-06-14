using Core.Ore;
using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.WorldMapTile;
using SinglePlay;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// マップ上をクリック長押しして一定時間が経てば採掘実行プロトコルを送る
    /// </summary>
    public class OreMapTileClickDetect : MonoBehaviour
    {
        [SerializeField] private ProgressBarView progressBarView;
        
        //今の所は一律3秒
        //TODO コンフィグに対応させる
        private const float MiningTime = 3.0f;
        
        private Camera _mainCamera;
        private SendMiningProtocol _sendMiningProtocol;
        private UIStateControl _uiStateControl; 
        //TODO 用語の統一が出来てないのでOreConfigをMapTileConfigに変更する
        private IOreConfig _oreConfig; 
        
        private MapTileObject _currentMapTileObject;
        private float _currentClickTime;
        
        [Inject]
        public void Construct(Camera mainCamera,SendMiningProtocol sendMiningProtocol,UIStateControl uiStateControl,SinglePlayInterface singlePlayInterface)
        {
            _mainCamera = mainCamera;
            _sendMiningProtocol = sendMiningProtocol;
            _uiStateControl = uiStateControl;
            
            _oreConfig = singlePlayInterface.OreConfig;
        }

        private void Update()
        {
            if (_uiStateControl.CurrentState != UIStateEnum.DeleteBar) return;
            
            if (_currentMapTileObject == null)
            {
                _currentMapTileObject = GetBlockClicked();
                _currentClickTime = 0;
                return;
            }
            var clickedObject = GetBlockClicked();
            if (clickedObject != _currentMapTileObject)
            {
                _currentMapTileObject = clickedObject;
                _currentClickTime = 0;
                return;
            }
            _currentClickTime += Time.deltaTime;
            //TODO 将来的に採掘時間をコンフィグから取得する
            var config = _oreConfig.Get(_currentMapTileObject.TileId);
            
            //todo 採掘中　みたいなステートをちゃんと管理して、同時に採掘しないようにする
            progressBarView.SetProgress(_currentClickTime / MiningTime);

            if (MiningTime <= _currentClickTime)
            {
                _sendMiningProtocol.Send(GetClickPosition());
                _currentClickTime = 0;
            }
        }

        private MapTileObject GetBlockClicked()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // マウスでクリックした位置にタイルマップがあるとき
            if (!InputManager.Playable.ScreenLeftClick.GetKey) return null;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            //TODo この辺のGameObjectのrayの取得をutiにまとめたい
            if (!Physics.Raycast(ray, out var hit,100,LayerConst.WithoutOnlyMapObjectLayerMask)) return null;
            var mapTile = hit.collider.GetComponent<MapTileObject>();
            if (mapTile == null) return null;

            return mapTile;
        }

        private Vector2Int GetClickPosition()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.Raycast(ray, out var hit,100,LayerConst.WithoutOnlyMapObjectLayerMask))
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