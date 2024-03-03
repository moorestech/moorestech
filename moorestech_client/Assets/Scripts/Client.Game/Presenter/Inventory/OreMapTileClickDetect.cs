using System.Threading;
using Cysharp.Threading.Tasks;
using Constant;
using Game.PlayerInventory.Interface;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
using MainGame.UnityView.WorldMapTile;
using ServerServiceProvider;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// TODO map tileとして削除予定
    ///  マップ上をクリック長押しして一定時間が経てば採掘実行プロトコルを送る
    /// </summary>
    public class OreMapTileClickDetect : MonoBehaviour
    {
        [SerializeField] private MiningObjectProgressbarPresenter miningObjectProgressbarPresenter;
        [SerializeField] private HotBarView hotBarView;

        private MapTileObject _currentClickingMapTileObject;
        private Camera _mainCamera;
        private ILocalPlayerInventory _localPlayerInventory;
        private UIStateControl _uiStateControl;
        
        private CancellationToken _gameObjectCancellationToken;
        
        private CancellationTokenSource _miningTokenSource = new();

        [Inject]
        public void Construct(Camera mainCamera, UIStateControl uiStateControl, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            _mainCamera = mainCamera;
            _uiStateControl = uiStateControl;

            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
        }

        private async UniTask Update()
        {
            var forcesMapTile = GetForcesMapTile();
            if (!IsStartMining()) return;

            await Mining();

            #region Internal

            bool IsStartMining()
            {
                if (miningObjectProgressbarPresenter.IsMining || _uiStateControl.CurrentState != UIStateEnum.GameScreen) return false;

                if (!InputManager.Playable.ScreenLeftClick.GetKey || forcesMapTile == null) return false;
                return true;
            }

            async UniTask Mining()
            {
                _miningTokenSource.Cancel();
                _miningTokenSource = new CancellationTokenSource();

                var miningTime = GetMiningTime();
                miningObjectProgressbarPresenter.StartMining(miningTime, _miningTokenSource.Token).Forget();


                //map objectがフォーカスされ、クリックされているので採掘を行う
                //採掘中はこのループの中にいる
                //採掘時間分ループする
                var nowTime = 0f;
                while (nowTime < miningTime)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _gameObjectCancellationToken);
                    nowTime += Time.deltaTime;

                    //クリックが離されたら採掘を終了する
                    //map objectが変わったら採掘を終了する
                    if (!InputManager.Playable.ScreenLeftClick.GetKeyUp && forcesMapTile == GetForcesMapTile()) continue;

                    return;
                }

                //MoorestechContext.VanillaApi.SendOnly.Send(GetClickPosition());
                _miningTokenSource.Cancel();
            }


            MapTileObject GetForcesMapTile()
            {
                var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
                var ray = _mainCamera.ScreenPointToRay(mousePosition);

                // UIのクリックかどうかを判定
                if (EventSystem.current.IsPointerOverGameObject()) return null;
                //TODo この辺のGameObjectのrayの取得をutiにまとめたい
                if (!Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutMapObjectAndPlayerLayerMask)) return null;
                var mapTile = hit.collider.GetComponent<MapTileObject>();
                if (mapTile == null) return null;

                return mapTile;
            }

            Vector2Int GetClickPosition()
            {
                var mousePosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
                var ray = _mainCamera.ScreenPointToRay(mousePosition);

                if (Physics.Raycast(ray, out var hit, 100, LayerConst.WithoutMapObjectAndPlayerLayerMask))
                {
                    var x = Mathf.RoundToInt(hit.point.x);
                    var y = Mathf.RoundToInt(hit.point.z);
                    return new Vector2Int(x, y);
                }

                return Vector2Int.zero;
            }


            float GetMiningTime()
            {
                var slotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarView.SelectIndex);

                //  TODO 将来的に採掘時間をコンフィグから取得する
                var stoneTool = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "stone tool", slotIndex);
                var ironPickaxe = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "iron pickaxe", slotIndex);
                if (ironPickaxe) return 3;

                if (stoneTool) return 7;

                return 100;
            }

            #endregion
        }
    }
}