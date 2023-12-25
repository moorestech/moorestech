using System.Threading;
using Core.Ore;
using Cysharp.Threading.Tasks;
using Constant;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
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
    ///     マップ上をクリック長押しして一定時間が経てば採掘実行プロトコルを送る
    /// </summary>
    public class OreMapTileClickDetect : MonoBehaviour
    {
        [SerializeField] private MiningObjectProgressbarPresenter miningObjectProgressbarPresenter;

        private MapTileObject _currentClickingMapTileObject;

        private CancellationToken _gameObjectCancellationToken;


        private Camera _mainCamera;

        private CancellationTokenSource _miningTokenSource = new();

        //TODO 用語の統一が出来てないのでOreConfigをMapTileConfigに変更する
        private IOreConfig _oreConfig;
        private IInventoryItems _inventoryItems;
        private SendMiningProtocol _sendMiningProtocol;
        private UIStateControl _uiStateControl;

        [Inject]
        public void Construct(Camera mainCamera, SendMiningProtocol sendMiningProtocol, UIStateControl uiStateControl, SinglePlayInterface singlePlayInterface)
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
            if (miningObjectProgressbarPresenter.IsMining || _uiStateControl.CurrentState != UIStateEnum.GameScreen) return;

            var forcesMapTile = GetForcesMapTile();
            if (!InputManager.Playable.ScreenLeftClick.GetKey || forcesMapTile == null) return;

            _miningTokenSource.Cancel();
            _miningTokenSource = new CancellationTokenSource();

            var miningTime = GetMiningTime(_inventoryItems);
            miningObjectProgressbarPresenter.StartMining(miningTime, _miningTokenSource.Token).Forget();

            var isMiningCanceled = false;

            //map objectがフォーカスされ、クリックされているので採掘を行う
            //採掘中はこのループの中にいる
            //採掘時間分ループする
            var nowTime = 0f;
            while (nowTime < miningTime)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                nowTime += Time.deltaTime;


                //クリックが離されたら採掘を終了する
                //map objectが変わったら採掘を終了する
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp || forcesMapTile != GetForcesMapTile())
                {
                    isMiningCanceled = true;
                    break;
                }
            }

            if (!isMiningCanceled) _sendMiningProtocol.Send(GetClickPosition());

            _miningTokenSource.Cancel();
        }


        private MapTileObject GetForcesMapTile()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            //TODo この辺のGameObjectのrayの取得をutiにまとめたい
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutOnlyMapObjectLayerMask)) return null;
            var mapTile = hit.collider.GetComponent<MapTileObject>();
            if (mapTile == null) return null;

            return mapTile;
        }

        private Vector2Int GetClickPosition()
        {
            var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutOnlyMapObjectLayerMask))
            {
                var x = Mathf.RoundToInt(hit.point.x);
                var y = Mathf.RoundToInt(hit.point.z);
                return new Vector2Int(x, y);
            }

            return Vector2Int.zero;
        }


        /// <summary>
        ///     マイニングする時間を取得する
        ///     TODO 将来的に採掘時間をコンフィグから取得する
        /// </summary>
        /// <param name="playerInventoryViewModel"></param>
        /// <returns></returns>
        private static float GetMiningTime(IInventoryItems inventoryItems)
        {
            var stoneTool = inventoryItems.IsItemExist(AlphaMod.ModId, "stone tool");
            var ironPickaxe = inventoryItems.IsItemExist(AlphaMod.ModId, "iron pickaxe");
            if (ironPickaxe) return 3;

            if (stoneTool) return 7;

            return 100;
        }
    }
}