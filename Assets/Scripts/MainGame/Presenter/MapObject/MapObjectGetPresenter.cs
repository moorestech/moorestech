using System.Threading;
using Core.Const;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using Game.MapObject.Interface;
using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectGetPresenter : MonoBehaviour
    {
        
        [SerializeField] private MiningObjectProgressbarPresenter miningObjectProgressbarPresenter;

        private PlayerInventoryViewModel _playerInventoryViewModel;
        private UIStateControl _uiStateControl;
        private SendGetMapObjectProtocolProtocol _sendGetMapObjectProtocolProtocol;
        private CancellationTokenSource _miningCancellationTokenSource = new();
        
        private CancellationToken _gameObjectCancellationToken;

        [Inject]
        public void Constructor(UIStateControl uiStateControl,SendGetMapObjectProtocolProtocol sendGetMapObjectProtocolProtocol,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _uiStateControl = uiStateControl;
            _sendGetMapObjectProtocolProtocol = sendGetMapObjectProtocolProtocol;
            _playerInventoryViewModel = playerInventoryViewModel;
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

            var forcesMapObject = GetOnMouseMapObject();
            if (miningObjectProgressbarPresenter.IsMining || !InputManager.Playable.ScreenLeftClick.GetKey || forcesMapObject == null)
            {
                return;
            }
            
            forcesMapObject.OutlineEnable(true);
            
            _miningCancellationTokenSource.Cancel();
            _miningCancellationTokenSource = new CancellationTokenSource();
            
            var miningTime = GetMiningTime(forcesMapObject.MapObjectType,_playerInventoryViewModel);
            
            //マイニングバーのUIを表示するやつを設定
            miningObjectProgressbarPresenter.StartMining(miningTime,_miningCancellationTokenSource.Token).Forget();

            var isMiningCanceled = false;
            
            //map objectがフォーカスされ、クリックされているので採掘を行う
            //採掘中はこのループの中にいる
            //採掘時間分ループする
            var nowTime = 0f;
            while (nowTime < miningTime)
            {
                await UniTask.Yield(PlayerLoopTiming.Update,cancellationToken);
                nowTime += Time.deltaTime;


                //クリックが離されたら採掘を終了する
                //map objectが変わったら採掘を終了する
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp || forcesMapObject != GetOnMouseMapObject())
                { 
                    isMiningCanceled = true;
                    break;
                }
            }

            //マイニングをキャンセルせずに終わったので、マイニング完了をサーバーに送信する
            if (!isMiningCanceled)
            {
                _sendGetMapObjectProtocolProtocol.Send(forcesMapObject.InstanceId);
                SoundEffectType soundEffectType;
                switch (forcesMapObject.MapObjectType)
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

            forcesMapObject.OutlineEnable(false);
            _miningCancellationTokenSource.Cancel();
        }

        private MapObjectGameObject GetOnMouseMapObject()
        {
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000)) return null;
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            
            //マップオブジェクトを取得する
            return hit.collider.gameObject.GetComponent<MapObjectGameObject>();
        }


        /// <summary>
        /// 採掘時間を取得する
        /// 採掘アイテムがインベントリにあれば早くなる
        /// </summary>
        private static float GetMiningTime(string mapObjectType,PlayerInventoryViewModel playerInv)
        {
            var isStoneTool = playerInv.IsItemExist(AlphaMod.ModId,"stone tool");
            var isStoneAx = playerInv.IsItemExist(AlphaMod.ModId,"stone ax");
            var isIronAx = playerInv.IsItemExist(AlphaMod.ModId,"iron ax");
            var isIronPickaxe = playerInv.IsItemExist(AlphaMod.ModId,"iron pickaxe");

            switch (mapObjectType)
            {
                #region 木
                case VanillaMapObjectType.VanillaTree when isIronAx:
                    return 4;
                case VanillaMapObjectType.VanillaTree when isStoneAx:
                    return 6;
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
                    return 6;
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