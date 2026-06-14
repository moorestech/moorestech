using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory
{
    public class HotBarView : MonoBehaviour
    {
        [SerializeField] private List<HotBarItem> hotBarItems;
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        public event Action<int> OnSelectHotBar;
        
        
        private GameObject _currentGrabItem;
        private CancellationTokenSource _loadCancellationTokenSource;
        private LoadedAsset<GameObject> _currentLoadedAsset;
        
        public IItemStack CurrentItem => _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(SelectIndex)];
        
        /// <summary>
        /// 0〜8のインデックス　インベントリ上のどのアイテムかは <see cref="PlayerInventoryConst.HotBarSlotToInventorySlot"/> を参照
        /// Index from 0 to 8. To find out which item in the inventory, refer to <see cref="PlayerInventoryConst.HotBarSlotToInventorySlot"/>.
        /// </summary>
        public int SelectIndex { get; private set; }
        private float _switchHotBarDeltaTotal;
        
        private void Start()
        {
            SelectIndex = 0;
            UpdateSelectedView(0, 0);
            for (var i = 0; i < hotBarItems.Count; i++)
            {
                var keyBordText = (i + 1).ToString();
                hotBarItems[i].SetKeyBoardText(keyBordText);
            }
        }
        
        private void Update()
        {
            UpdateHotBarItem();
            var nextSelectIndex = SelectedHotBar();
            if (nextSelectIndex != -1 && nextSelectIndex != SelectIndex)
            {
                // キーボード/スクロール経路。外部経路と同じ選択変更処理へ集約する
                // Keyboard/scroll path; routed through the same selection-change handling as the external path
                ApplySelection(nextSelectIndex);
            }
            
            #region Internal
            
            void UpdateHotBarItem()
            {
                for (var i = 0; i < _localPlayerInventory.Count; i++) UpdateHotBarElement(i, _localPlayerInventory[i]);
            }
            
            void UpdateHotBarElement(int slot, IItemStack item)
            {
                //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
                var c = PlayerInventoryConst.MainInventoryColumns;
                var r = PlayerInventoryConst.MainInventoryRows;
                var startHotBarSlot = c * (r - 1);
                
                if (slot < startHotBarSlot || PlayerInventoryConst.MainInventorySize <= slot) return;
                
                slot -= startHotBarSlot;
                // 同じアイテムなら更新しない
                if (hotBarItems[slot].ItemId == item.Id) return;
                
                var viewData = ClientContext.ItemImageContainer.GetItemView(item.Id);
                hotBarItems[slot].SetItem(viewData, item.Count);
            }
            
            int SelectedHotBar()
            {
                // スクロールで変化
                _switchHotBarDeltaTotal += InputManager.UI.SwitchHotBar.ReadValue<float>() / 100f;
                
                if (_switchHotBarDeltaTotal > 1)
                {
                    var s = Mathf.FloorToInt(_switchHotBarDeltaTotal);
                    _switchHotBarDeltaTotal -= s;
                    var selected = SelectIndex + s;
                    selected = (selected + hotBarItems.Count) % hotBarItems.Count;
                    return selected;
                }
                if (_switchHotBarDeltaTotal < -1)
                {
                    var s = Mathf.CeilToInt(_switchHotBarDeltaTotal);
                    _switchHotBarDeltaTotal -= s;
                    var selected = SelectIndex + s;
                    selected = (selected + hotBarItems.Count) % hotBarItems.Count;
                    return selected;
                }


                //キーボード入力で選択
                if (InputManager.UI.HotBar.ReadValue<int>() == 0) return -1;

                {
                    //キー入力で得られる値は1〜9なので-1する
                    var selected = InputManager.UI.HotBar.ReadValue<int>() - 1;
                    return selected;
                }
            }

            #endregion
        }


        private void UpdateSelectedView(int prevIndex, int nextIndex)
        {
            hotBarItems[prevIndex].SetSelect(false);
            hotBarItems[nextIndex].SetSelect(true);
        }

        // 選択変更の共通処理。ハイライト/手持ち3Dモデル/event/SelectIndex を同時更新する
        // Shared selection-change handling; updates highlight, held 3D model, event, and SelectIndex together
        private void ApplySelection(int nextIndex)
        {
            UpdateSelectedView(SelectIndex, nextIndex);
            UpdateHoldItemAsync(nextIndex).Forget(); //アイテムの再生成があるので変化を検知して変更する
            OnSelectHotBar?.Invoke(nextIndex);
            SelectIndex = nextIndex;
        }

        // Web UI など外部から選択スロットを設定する
        // Set the selected slot from outside (e.g. Web UI)
        public void SetSelectIndex(int index)
        {
            // 範囲外入力は丸める。選択が変わらない場合は何もしない
            // Clamp out-of-range input; do nothing when the selection does not change
            var clamped = (index % hotBarItems.Count + hotBarItems.Count) % hotBarItems.Count;
            if (clamped == SelectIndex) return;

            ApplySelection(clamped);
        }

        private async UniTaskVoid UpdateHoldItemAsync(int selectIndex)
        {
            // 既存のロード処理をキャンセル
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = new CancellationTokenSource();

            // 既存のアイテムをクリーンアップ
            if (_currentGrabItem != null)
            {
                Destroy(_currentGrabItem.gameObject);
                _currentGrabItem = null;
            }

            // Addressableリソースを解放
            _currentLoadedAsset?.Dispose();
            _currentLoadedAsset = null;

            var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;

            if (itemId == ItemMaster.EmptyItemId) return;

            try
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                var token = _loadCancellationTokenSource.Token;

                // handGrabModelが設定されているかチェック
                // Check if handGrabModel is set
                if (!string.IsNullOrEmpty(itemMaster.AddressablePaths?.HandGrabModel))
                {
                    // Addressableからロード
                    // Load from Addressable
                    _currentLoadedAsset = await AddressableLoader.LoadAsync<GameObject>(itemMaster.AddressablePaths.HandGrabModel);

                    if (token.IsCancellationRequested) return;

                    if (_currentLoadedAsset?.Asset != null)
                    {
                        _currentGrabItem = Instantiate(_currentLoadedAsset.Asset);
                        PlayerSystemContainer.Instance.PlayerGrabItemManager.SetItem(_currentGrabItem, false);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load hand grab model for item {itemId}: {e.Message}");
            }
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void OnDestroy()
        {
            // キャンセルトークンソースをクリーンアップ
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            
            // Addressableリソースを解放
            _currentLoadedAsset?.Dispose();
            
            // ゲームオブジェクトを破棄
            if (_currentGrabItem != null)
            {
                Destroy(_currentGrabItem);
            }
        }
    }
}