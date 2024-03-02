using System.Collections.Generic;
using System.Threading;
using Client.Game.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.MapObject.Interface;
using Constant;
using Game.PlayerInventory.Interface;
using MainGame.UnityView.Control;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.Player;
using MainGame.UnityView.SoundEffect;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.UI.Util;
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
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private float miningDistance = 1.5f;

        private CancellationToken _gameObjectCancellationToken;
        private CancellationTokenSource _miningCancellationTokenSource = new();

        private ILocalPlayerInventory _localPlayerInventory;
        private UIStateControl _uiStateControl;
        private IPlayerObjectController _playerObjectController;

        [Inject]
        public void Constructor(UIStateControl uiStateControl, ILocalPlayerInventory localPlayerInventory, IPlayerObjectController playerObjectController)
        {
            _uiStateControl = uiStateControl;
            _localPlayerInventory = localPlayerInventory;
            _playerObjectController = playerObjectController;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
        }
        
        private MapObjectGameObject _currentMapObjectGameObject = null;

        private async UniTask Update()
        {
            if (miningObjectProgressbarPresenter.IsMining)
            {
                return;
            }
            
            UpdateCurrentMapObject();
            var isMinenable = IsStartMining();

            if (_currentMapObjectGameObject != null)
            {
                var text = string.Empty;
                if (isMinenable)
                {
                    text = "Press and hold left-click to get";
                }
                else
                {
                    text = "このアイテムが必要です:" + string.Join(", ", GetRecommendItemId(_currentMapObjectGameObject.MapObjectType));
                }
                
                MouseCursorExplainer.Instance.Show(text,isLocalize:isMinenable);
            }
            
            if (!isMinenable)
            {
                return;
            }

            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                return;
            }

            await Mining();
            

            #region Internal

            bool IsStartMining()
            {
                if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return false;
                
                if (_currentMapObjectGameObject == null) return false;
            
                var (_,mineable) = GetMiningData(_currentMapObjectGameObject.MapObjectType);

                if (!mineable) return false;

                return true;
            }

            async UniTask Mining()
            {
                _miningCancellationTokenSource.Cancel();
                _miningCancellationTokenSource = new CancellationTokenSource();

                //マイニングバーのUIを表示するやつを設定
                var (miningTime,_) = GetMiningData(_currentMapObjectGameObject.MapObjectType);
                miningObjectProgressbarPresenter.StartMining(miningTime, _miningCancellationTokenSource.Token).Forget();
                
                //_playerObjectController.SetAnimationState(PlayerAnimationState.Axe);

                var isMiningFinish = await IsMiningFinishWait(miningTime);
                
                _playerObjectController.SetAnimationState(PlayerAnimationState.IdleWalkRunBlend);

                //マイニングをキャンセルせずに終わったので、マイニング完了をサーバーに送信する
                if (isMiningFinish)
                {
                    MoorestechContext.VanillaApi.SendOnly.GetMapObject(_currentMapObjectGameObject.InstanceId);
                    PlaySoundEffect();
                }

                _miningCancellationTokenSource.Cancel();
            }

            void UpdateCurrentMapObject()
            {
                var mapObject = GetOnMouseMapObject();
                if (mapObject == null)
                {
                    if (_currentMapObjectGameObject != null)
                    {
                        MouseCursorExplainer.Instance.Hide();
                        _currentMapObjectGameObject.OutlineEnable(false);
                    }
                    _currentMapObjectGameObject = null;
                    return;
                }

                if (_currentMapObjectGameObject == mapObject) return;
                
                if (_currentMapObjectGameObject != null)
                {
                    _currentMapObjectGameObject.OutlineEnable(false);
                }
                _currentMapObjectGameObject = mapObject;
                _currentMapObjectGameObject.OutlineEnable(true);
            }

            (float miningTime, bool mineable) GetMiningData(string mapObjectType)
            {
                var slotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarView.SelectIndex);

                //TODO 採掘するためのアイテムはコンフィグに移す（mapObject.jsonとか作る？）
                var isStoneTool = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "stone tool", slotIndex);
                var isStoneAx = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "stone ax", slotIndex);
                var isIronAx = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "iron ax", slotIndex);
                var isIronPickaxe = _localPlayerInventory.IsItemExist(AlphaMod.ModId, "iron pickaxe", slotIndex);

                switch (mapObjectType)
                {
                    #region 木

                    case VanillaMapObjectType.VanillaTree when isIronAx:
                        return (4, true);
                    case VanillaMapObjectType.VanillaTree when isStoneAx:
                        return (4, true);
                    case VanillaMapObjectType.VanillaTree when isStoneTool:
                        return (10, true);

                    case VanillaMapObjectType.VanillaBigTree when isIronAx:
                        return (10, true);

                    #endregion

                    #region 石

                    case VanillaMapObjectType.VanillaStone:
                        return (5, true);


                    case VanillaMapObjectType.VanillaCoal when isIronPickaxe:
                        return (5, true);
                    case VanillaMapObjectType.VanillaIronOre when isIronPickaxe:
                        return (10, true);
                    case VanillaMapObjectType.VanillaCray:
                        return (3, true);

                    #endregion

                    #region ブッシュ

                    case VanillaMapObjectType.VanillaBush:
                        return (3, true);

                    #endregion
                }

                return (5, false);
            }

            List<string> GetRecommendItemId(string mapObjectType)
            {
                return mapObjectType switch
                {
                    VanillaMapObjectType.VanillaTree => new List<string> {"iron ax", "stone ax", "stone tool"},
                    VanillaMapObjectType.VanillaBigTree => new List<string> {"iron ax"},
                    VanillaMapObjectType.VanillaCoal => new List<string> {"iron pickaxe"},
                    VanillaMapObjectType.VanillaIronOre => new List<string> {"iron pickaxe"},
                    _ => new List<string>()
                };
            }
            
            async UniTask<bool> IsMiningFinishWait(float miningTime)
            {
                //map objectがフォーカスされ、クリックされているので採掘を行う
                //採掘中はこのループの中にいる
                //採掘時間分ループする
                var nowTime = 0f;
                while (nowTime < miningTime)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _gameObjectCancellationToken);
                    nowTime += Time.deltaTime;

                    //クリックが離されたら採掘を終了する か map objectが変わったら採掘を終了する
                    if (InputManager.Playable.ScreenLeftClick.GetKeyUp || _currentMapObjectGameObject != GetOnMouseMapObject())
                    {
                        return false;
                    }
                }

                return true;
            }

            void PlaySoundEffect()
            {
                SoundEffectType soundEffectType;
                switch (_currentMapObjectGameObject.MapObjectType)
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
            
            #endregion
        }

        private MapObjectGameObject GetOnMouseMapObject()
        {
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 10)) return null;
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            if (!hit.collider.gameObject.TryGetComponent(out MapObjectGameObject mapObject)) return null;
                
            var playerPos = _playerObjectController.Position;
            var mapObjectPos = mapObject.transform.position;
            if (miningDistance < Vector3.Distance(playerPos, mapObjectPos)) return null;

            return mapObject;
        }



    }
}