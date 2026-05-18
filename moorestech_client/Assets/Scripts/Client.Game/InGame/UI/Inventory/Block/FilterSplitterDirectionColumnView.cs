using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// フィルター分岐器の1出力方向ぶんのUI。モード切替と複数のフィルタースロットを管理する。
    /// One direction column of the filter splitter UI: mode toggle + filter slots.
    /// </summary>
    public class FilterSplitterDirectionColumnView : MonoBehaviour
    {
        [SerializeField] private TMP_Text directionLabel;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private Button modeButton;
        [SerializeField] private Transform filterSlotsParent;

        // モードが切り替えられたとき
        // Fired when mode is toggled
        public IObservable<FilterSplitterMode> OnModeCycleRequested => _modeCycleSubject;
        // フィルタースロットがクリックされたとき (slotIndex, leftClick:true / rightClick:false)
        // Fired when a filter slot is clicked
        public IObservable<(int slotIndex, bool isLeftClick)> OnFilterSlotClicked => _slotClickSubject;

        private readonly Subject<FilterSplitterMode> _modeCycleSubject = new();
        private readonly Subject<(int, bool)> _slotClickSubject = new();
        private readonly List<ItemSlotView> _slots = new();
        private readonly CompositeDisposable _slotSubscriptions = new();

        private FilterSplitterMode _currentMode = FilterSplitterMode.Default;

        public void Build(int directionIndex, int filterSlotCount)
        {
            if (directionLabel != null) directionLabel.text = $"出力 {directionIndex + 1}";

            // モードボタンの押下でクリック通知（モード自体は親から受け取る）
            // Button click only notifies; mode value is updated by parent
            modeButton.onClick.AddListener(() => _modeCycleSubject.OnNext(NextMode(_currentMode)));

            // フィルタースロットを生成して購読
            // Spawn filter slots and subscribe to their click events
            for (var i = 0; i < filterSlotCount; i++)
            {
                var slot = Instantiate(ItemSlotView.Prefab, filterSlotsParent);
                slot.SetItem(null, 0);
                var capturedIndex = i;
                slot.OnLeftClickUp
                    .Subscribe(_ => _slotClickSubject.OnNext((capturedIndex, true)))
                    .AddTo(_slotSubscriptions);
                slot.OnRightClickUp
                    .Subscribe(_ => _slotClickSubject.OnNext((capturedIndex, false)))
                    .AddTo(_slotSubscriptions);
                _slots.Add(slot);
            }
        }

        public void ApplyState(FilterSplitterMode mode, IReadOnlyList<string> filterItemGuids)
        {
            _currentMode = mode;
            modeLabel.text = mode switch
            {
                FilterSplitterMode.Default => "Default",
                FilterSplitterMode.Whitelist => "Whitelist",
                FilterSplitterMode.Blacklist => "Blacklist",
                _ => "?",
            };

            // 各スロットに対応するアイテムをセット
            // Apply item view to each filter slot
            for (var i = 0; i < _slots.Count; i++)
            {
                if (i >= filterItemGuids.Count || string.IsNullOrEmpty(filterItemGuids[i]))
                {
                    _slots[i].SetItem(null, 0);
                    continue;
                }
                if (!Guid.TryParse(filterItemGuids[i], out var guid) || guid == Guid.Empty)
                {
                    _slots[i].SetItem(null, 0);
                    continue;
                }
                var idOrNull = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
                if (idOrNull == null)
                {
                    _slots[i].SetItem(null, 0);
                    continue;
                }
                var view = Client.Game.InGame.Context.ClientContext.ItemImageContainer.GetItemView(idOrNull.Value);
                _slots[i].SetItem(view, 0);
            }
        }

        private void OnDestroy()
        {
            _slotSubscriptions.Dispose();
            _modeCycleSubject.Dispose();
            _slotClickSubject.Dispose();
        }

        private static FilterSplitterMode NextMode(FilterSplitterMode current)
        {
            return current switch
            {
                FilterSplitterMode.Default => FilterSplitterMode.Whitelist,
                FilterSplitterMode.Whitelist => FilterSplitterMode.Blacklist,
                FilterSplitterMode.Blacklist => FilterSplitterMode.Default,
                _ => FilterSplitterMode.Default,
            };
        }
    }
}
