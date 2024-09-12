using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.Util;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Map.Interface.Config;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectGetPresenter : MonoBehaviour
    {
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private float miningDistance = 1.5f;
        
        private MapObjectGameObject _currentMapObjectGameObject;
        
        private CancellationToken _gameObjectCancellationToken;
        
        private ILocalPlayerInventory _localPlayerInventory;
        private IPlayerObjectController _playerObjectController;
        private UIStateControl _uiStateControl;
        
        
        private void Start()
        {
            ManualUpdate().Forget();
        }
        
        private async UniTask ManualUpdate()
        {
            while (true)
            {
                await MiningUpdate();
                await UniTask.Yield(PlayerLoopTiming.Update, _gameObjectCancellationToken);
            }
        }
        
        private async UniTask MiningUpdate()
        {
            UpdateCurrentMapObject();
            
            var miningToolInfo = GetMiningToolInfo();
            var isMinenable = IsStartMining();
            var isPickUpable = false;
            
            if (_currentMapObjectGameObject != null)
            {
                var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(_currentMapObjectGameObject.MapObjectGuid);
                isPickUpable = mapObjectConfig.MiningTools.Count == 0;
                var text = string.Empty;
                if (isMinenable)
                    text = isPickUpable ? "左クリックで取得" : "左クリック長押しで採掘";
                else
                    text = "このアイテムが必要です:" + string.Join(", ", GetRecommendItemId(_currentMapObjectGameObject.MapObjectGuid));
                
                MouseCursorExplainer.Instance.Show(text, isLocalize: false);
            }
            
            if (!isMinenable) return;
            
            if (!InputManager.Playable.ScreenLeftClick.GetKey) return;
            
            await Mining();
            
            #region Internal
            
            void UpdateCurrentMapObject()
            {
                var mapObject = GetMapObject();
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
                
                if (_currentMapObjectGameObject != null) _currentMapObjectGameObject.OutlineEnable(false);
                _currentMapObjectGameObject = mapObject;
                _currentMapObjectGameObject.OutlineEnable(true);
            }
            
            bool IsStartMining()
            {
                if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return false;
                
                if (_currentMapObjectGameObject == null) return false;
                
                
                var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(_currentMapObjectGameObject.MapObjectGuid);
                
                return miningToolInfo != null || mapObjectConfig.MiningTools.Count == 0;
            }
            
            async UniTask Mining()
            {
                var instanceId = _currentMapObjectGameObject.InstanceId;
                
                if (isPickUpable)
                {
                    ClientContext.VanillaApi.SendOnly.AttackMapObject(instanceId, int.MaxValue); //TODO max valueじゃないものにしたい
                    return;
                }
                
                //マイニングバーのUIを表示するやつを設定
                _playerObjectController.SetAnimationState(PlayerAnimationState.Axe);
                
                var isMiningFinish = await IsMiningFinishWait(miningToolInfo.AttackSpeed);
                
                _playerObjectController.SetAnimationState(PlayerAnimationState.IdleWalkRunBlend);
                
                //マイニングをキャンセルせずに終わったので、マイニング完了をサーバーに送信する
                if (isMiningFinish)
                {
                    var damage = miningToolInfo.Damage;
                    ClientContext.VanillaApi.SendOnly.AttackMapObject(instanceId, damage);
                    PlaySoundEffect();
                }
            }
            
            MapObjectToolItemConfigInfo GetMiningToolInfo()
            {
                if (_currentMapObjectGameObject == null) return null;
                
                var slotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarView.SelectIndex);
                var currentItem = _localPlayerInventory[slotIndex];
                
                var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(_currentMapObjectGameObject.MapObjectGuid);
                
                return mapObjectConfig.MiningTools.FirstOrDefault(tool => tool.ToolItemId == currentItem.Id);
            }
            
            List<string> GetRecommendItemId(string mapObjectType)
            {
                var mapObjectConfig = ServerContext.MapObjectConfig.GetConfig(mapObjectType);
                var result = new List<string>();
                foreach (var tool in mapObjectConfig.MiningTools)
                {
                    var itemConfig = ServerContext.ItemConfig.GetItemConfig(tool.ToolItemId);
                    result.Add(itemConfig.Name);
                }
                
                return result;
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
                    if (InputManager.Playable.ScreenLeftClick.GetKeyUp || _currentMapObjectGameObject != GetMapObject()) return false;
                }
                
                return true;
            }
            
            void PlaySoundEffect()
            {
                SoundEffectType soundEffectType;
                switch (_currentMapObjectGameObject.MapObjectGuid)
                {
                    case VanillaMapObjectType.VanillaPebble:
                        soundEffectType = SoundEffectType.DestroyStone;
                        break;
                    case VanillaMapObjectType.VanillaTree:
                        soundEffectType = SoundEffectType.DestroyTree;
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
        
        [Inject]
        public void Constructor(UIStateControl uiStateControl, ILocalPlayerInventory localPlayerInventory, IPlayerObjectController playerObjectController)
        {
            _uiStateControl = uiStateControl;
            _localPlayerInventory = localPlayerInventory;
            _playerObjectController = playerObjectController;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
        }
        
        private MapObjectGameObject GetMapObject()
        {
            if (Camera.main == null) return null;
            
            var ray = Camera.main.ScreenPointToRay(new Vector2(Screen.width / 2.0f, Screen.height / 2.0f));
            if (!Physics.Raycast(ray, out var hit, 10, LayerConst.MapObjectOnlyLayerMask)) return null;
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            if (!hit.collider.gameObject.TryGetComponent(out MapObjectGameObject mapObject)) return null;
            
            var playerPos = _playerObjectController.Position;
            var mapObjectPos = mapObject.transform.position;
            if (miningDistance < Vector3.Distance(playerPos, mapObjectPos)) return null;
            
            return mapObject;
        }
    }
}