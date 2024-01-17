using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MapObject.Interface;
using Constant;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.Game;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    ///     マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectGetPresenter : MonoBehaviour
    {
        [SerializeField] private MiningObjectProgressbarPresenter miningObjectProgressbarPresenter;
        [SerializeField] private float miningDistance = 1.5f;

        private CancellationToken _gameObjectCancellationToken;
        private CancellationTokenSource _miningCancellationTokenSource = new();

        private ILocalPlayerInventory _localPlayerInventory;
        private SendGetMapObjectProtocolProtocol _sendGetMapObjectProtocolProtocol;
        private UIStateControl _uiStateControl;
        private IPlayerPosition _playerPosition;

        [Inject]
        public void Constructor(UIStateControl uiStateControl, SendGetMapObjectProtocolProtocol sendGetMapObjectProtocolProtocol, ILocalPlayerInventory localPlayerInventory, IPlayerPosition playerPosition)
        {
            _uiStateControl = uiStateControl;
            _sendGetMapObjectProtocolProtocol = sendGetMapObjectProtocolProtocol;
            _localPlayerInventory = localPlayerInventory;
            _playerPosition = playerPosition;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
        }
        
        private MapObjectGameObject _lastMapObjectGameObject = null;

        private async UniTask Update()
        {
            if (_uiStateControl.CurrentState != UIStateEnum.SelectHotBar) return;

            var mapObject = GetOnMouseMapObject();
            if (mapObject == null)
            {
                if (_lastMapObjectGameObject != null)
                {
                    _lastMapObjectGameObject.OutlineEnable(false);
                }
                _lastMapObjectGameObject = null;
                return;
            }

            if (_lastMapObjectGameObject != mapObject)
            {
                if (_lastMapObjectGameObject != null)
                {
                    _lastMapObjectGameObject.OutlineEnable(false);
                }
                _lastMapObjectGameObject = mapObject;
                _lastMapObjectGameObject.OutlineEnable(true);
            }

            if (miningObjectProgressbarPresenter.IsMining || !InputManager.Playable.ScreenLeftClick.GetKey) return;
            
            _miningCancellationTokenSource.Cancel();
            _miningCancellationTokenSource = new CancellationTokenSource();

            var miningTime = GetMiningTime(_lastMapObjectGameObject.MapObjectType);

            //マイニングバーのUIを表示するやつを設定
            miningObjectProgressbarPresenter.StartMining(miningTime, _miningCancellationTokenSource.Token).Forget();

            var isMiningCanceled = false;

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
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp || _lastMapObjectGameObject != GetOnMouseMapObject())
                {
                    isMiningCanceled = true;
                    break;
                }
            }

            //マイニングをキャンセルせずに終わったので、マイニング完了をサーバーに送信する
            if (!isMiningCanceled)
            {
                _sendGetMapObjectProtocolProtocol.Send(_lastMapObjectGameObject.InstanceId);
                SoundEffectType soundEffectType;
                switch (_lastMapObjectGameObject.MapObjectType)
                {
                    case VanillaMapObjectType.VanillaStone:
                    case VanillaMapObjectType.VanillaCray:
                    case VanillaMapObjectType.VanillaCoal:
                    case VanillaMapObjectType.VanillaIronOre:
                        soundEffectType = SoundEffectType.DestroyStone;
                        break;
                    case VanillaMapObjectType.VanillaTree:
                    case VanillaMapObjectType.VanillaBigTree:
                        soundEffectType = SoundEffectType.DestroyTree;
                        break;
                    case VanillaMapObjectType.VanillaBush:
                        soundEffectType = SoundEffectType.DestroyBush;
                        break;
                    default:
                        soundEffectType = SoundEffectType.DestroyStone;
                        Debug.LogError("採掘音が設定されていません");
                        break;
                }

                SoundEffectManager.Instance.PlaySoundEffect(soundEffectType);
            }

            _lastMapObjectGameObject.OutlineEnable(false);
            _miningCancellationTokenSource.Cancel();
        }

        private MapObjectGameObject GetOnMouseMapObject()
        {
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 10)) return null;
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            if (!hit.collider.gameObject.TryGetComponent(out MapObjectGameObject mapObject)) return null;
                
            var playerPos = _playerPosition.GetPlayerPosition3D();
            var mapObjectPos = mapObject.transform.position;
            if (miningDistance < Vector3.Distance(playerPos, mapObjectPos)) return null;

            return mapObject;
        }


        /// <summary>
        ///     採掘時間を取得する
        ///     採掘アイテムがインベントリにあれば早くなる
        /// </summary>
        private float GetMiningTime(string mapObjectType)
        {
            var isStoneTool = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "stone tool");
            var isStoneAx = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "stone ax");
            var isIronAx = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "iron ax");
            var isIronPickaxe = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "iron pickaxe");

            switch (mapObjectType)
            {
                #region 木

                case VanillaMapObjectType.VanillaTree when isIronAx:
                    return 4;
                case VanillaMapObjectType.VanillaTree when isStoneAx:
                    return 4;
                case VanillaMapObjectType.VanillaTree when isStoneTool:
                    return 10;
                case VanillaMapObjectType.VanillaTree:
                    return 10000;

                case VanillaMapObjectType.VanillaBigTree when isIronAx:
                    return 10;
                case VanillaMapObjectType.VanillaBigTree:
                    return 10000;

                #endregion

                #region 石

                case VanillaMapObjectType.VanillaStone:
                    return 5;


                case VanillaMapObjectType.VanillaCoal when isIronPickaxe:
                    return 5;
                case VanillaMapObjectType.VanillaCoal:
                    return 10000;
                case VanillaMapObjectType.VanillaIronOre when isIronPickaxe:
                    return 10;
                case VanillaMapObjectType.VanillaIronOre:
                    return 10000;

                case VanillaMapObjectType.VanillaCray when isStoneAx:
                    return 3;
                case VanillaMapObjectType.VanillaCray:
                    return 10000;

                #endregion

                #region ブッシュ

                case VanillaMapObjectType.VanillaBush:
                    return 3;

                #endregion
            }

            return 5;
        }
    }
}