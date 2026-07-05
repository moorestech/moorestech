using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     メインインベントリのスロットビューをスロット数に応じて動的生成する
    ///     Dynamically creates main inventory slot views to match the slot count
    ///     ホットバー行の9スロットは静的参照で、常にリスト末尾に配置される
    ///     The nine hotbar-row slots are static references kept at the tail of the list
    /// </summary>
    public class PlayerInventoryMainSlotsView : MonoBehaviour
    {
        public IReadOnlyList<ItemSlotView> SlotViews => _slotViews;
        public IObservable<ItemSlotView> OnSlotViewCreated => _onSlotViewCreated;

        [SerializeField] private ItemSlotView itemSlotViewPrefab;
        [SerializeField] private Transform slotsParent;
        [SerializeField] private List<ItemSlotView> hotBarSlotViews;

        private readonly List<ItemSlotView> _slotViews = new();
        private readonly Subject<ItemSlotView> _onSlotViewCreated = new();

        public void SetSlotCount(int slotCount)
        {
            // 初回はホットバー行の静的スロットを末尾として登録する
            // First call registers the static hotbar-row slots as the tail
            if (_slotViews.Count == 0)
            {
                foreach (var hotBarSlotView in hotBarSlotViews)
                {
                    _slotViews.Add(hotBarSlotView);
                    _onSlotViewCreated.OnNext(hotBarSlotView);
                }
            }

            // 不足分だけ生成しホットバー行の手前に挿入する。縮小（レベルダウン）は仕様上発生しない
            // Create only the missing views before the hotbar tail; shrinking never happens by design
            while (_slotViews.Count < slotCount)
            {
                var slotView = Instantiate(itemSlotViewPrefab, slotsParent);
                _slotViews.Insert(_slotViews.Count - hotBarSlotViews.Count, slotView);
                _onSlotViewCreated.OnNext(slotView);
            }
        }
    }
}
