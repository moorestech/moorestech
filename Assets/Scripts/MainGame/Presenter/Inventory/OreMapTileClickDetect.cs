using System.Threading;
using Core.Ore;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
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
        [SerializeField] private MiningObjectHelper miningObjectHelper;
        
        //今の所は一律3秒
        //TODO コンフィグに対応させる
        //TODO 将来的に採掘時間をコンフィグから取得する
        private const float MiningTime = 3.0f;
        
        private Camera _mainCamera;
        private SendMiningProtocol _sendMiningProtocol;
        private UIStateControl _uiStateControl; 
        //TODO 用語の統一が出来てないのでOreConfigをMapTileConfigに変更する
        private IOreConfig _oreConfig; 
        
        private MapTileObject _currentClickingMapTileObject;

        private CancellationTokenSource _miningTokenSource = new();
        
        private CancellationToken _gameObjectCancellationToken;
        
        [Inject]
        public void Construct(Camera mainCamera,SendMiningProtocol sendMiningProtocol,UIStateControl uiStateControl,SinglePlayInterface singlePlayInterface)
        {
            _mainCamera = mainCamera;
            _sendMiningProtocol = sendMiningProtocol;
            _uiStateControl = uiStateControl;
            
            _oreConfig = singlePlayInterface.OreConfig;
            
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            WhileUpdate().Forget();
        }

        
        
        private async UniTask WhileUpdate()
        {
            while (true)
            {
                await MiningUpdate(_gameObjectCancellationToken);
                await UniTask.Yield(_gameObjectCancellationToken);
            }
        }

        private async UniTask MiningUpdate(CancellationToken cancellationToken)
        {
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen)
            {
                return;
            }

            var forcesMapTile = GetForcesMapTile();
            if (!InputManager.Playable.ScreenLeftClick.GetKeyDown || forcesMapTile == null)
            {
                return;
            }
            
            _miningTokenSource.Cancel();
            _miningTokenSource = new CancellationTokenSource();
            miningObjectHelper.StartMining(MiningTime,_miningTokenSource.Token).Forget();

            var isMiningCanceled = false;
            
            //map objectがフォーカスされ、クリックされているので採掘を行う
            //採掘中はこのループの中にいる
            //採掘時間分ループする
            var nowTime = 0f;
            while (nowTime < MiningTime)
            {
                await UniTask.Yield(PlayerLoopTiming.Update,cancellationToken);
                nowTime += Time.deltaTime;


                //クリックが離されたら採掘を終了する
                //map objectが変わったら採掘を終了する
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp || forcesMapTile != GetForcesMapTile())
                { 
                    isMiningCanceled = true;
                }
            }

            if (!isMiningCanceled)
            {
                _sendMiningProtocol.Send(GetClickPosition());
            }

            _miningTokenSource.Cancel();
        }


        private MapTileObject GetForcesMapTile()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

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