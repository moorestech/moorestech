using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory
{
    public class HotBarView : MonoBehaviour
    {
        [SerializeField] private List<HotBarItem> hotBarItems;
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        public event Action<int> OnSelectHotBar;

        // 手持ち3Dモデルのロード/破棄を担う非MonoBehaviourヘルパー
        // Non-MonoBehaviour helper that loads/disposes the held 3D model
        private HotBarHeldItemModel _heldItemModel;

        public IItemStack CurrentItem => _localPlayerInventory[_localPlayerInventory.GetHotBarInventorySlot(SelectIndex)];
        
        /// <summary>
        /// 0〜8のインデックス　インベントリ上のどのアイテムかは <see cref="PlayerInventoryConst.HotBarSlotToInventorySlot"/> を参照
        /// Index from 0 to 8. To find out which item in the inventory, refer to <see cref="PlayerInventoryConst.HotBarSlotToInventorySlot"/>.
        /// </summary>
        public int SelectIndex { get; private set; }
        private bool _isInitialized;
        private bool _isGameStateVisible = true;
        private float _switchHotBarDeltaTotal;

        private void Awake()
        {
            // 実効モード変化を旧表示へ反映する
            // Reflect effective-mode changes in the legacy view
            WebUiScreenGate.OnWebUiModeChanged
                .Subscribe(_ => ApplyVisibility())
                .AddTo(this);
        }

        private void Start()
        {
            _heldItemModel = new HotBarHeldItemModel(_localPlayerInventory);

            SelectIndex = 0;
            UpdateSelectedView(0, 0);
            for (var i = 0; i < hotBarItems.Count; i++)
            {
                var keyBordText = (i + 1).ToString();
                hotBarItems[i].SetKeyBoardText(keyBordText);
            }

            _isInitialized = true;
            ApplyVisibility();
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
                if (!_localPlayerInventory.IsHotBarSlot(slot)) return;

                slot -= _localPlayerInventory.GetHotBarInventorySlot(0);
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
            _heldItemModel.UpdateAsync(nextIndex).Forget(); //アイテムの再生成があるので変化を検知して変更する
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

        public void SetActive(bool active)
        {
            _isGameStateVisible = active;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // 初期化後に表示要求とWebUIを合成する
            // Combine the visibility request with Web UI after initialization
            if (!_isInitialized) return;
            gameObject.SetActive(_isGameStateVisible && !WebUiScreenGate.IsWebUiMode);
        }
        
        private void OnDestroy()
        {
            // 手持ちモデルのロード/リソースをまとめて破棄する
            // Dispose the held-model load and resources together
            _heldItemModel?.Dispose();
        }
    }
}
