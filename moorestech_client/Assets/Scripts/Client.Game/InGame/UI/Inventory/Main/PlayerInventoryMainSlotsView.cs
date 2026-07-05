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
    /// </summary>
    public class PlayerInventoryMainSlotsView : MonoBehaviour
    {
        public IReadOnlyList<ItemSlotView> SlotViews => _slotViews;
        public IObservable<ItemSlotView> OnSlotViewCreated => _onSlotViewCreated;

        [SerializeField] private ItemSlotView itemSlotViewPrefab;
        [SerializeField] private Transform slotsParent;

        private readonly List<ItemSlotView> _slotViews = new();
        private readonly Subject<ItemSlotView> _onSlotViewCreated = new();

        public void SetSlotCount(int slotCount)
        {
            // 不足分だけ生成する。縮小（レベルダウン）は仕様上発生しない
            // Create only the missing views; shrinking never happens by design
            while (_slotViews.Count < slotCount)
            {
                var slotView = Instantiate(itemSlotViewPrefab, slotsParent);
                _slotViews.Add(slotView);
                _onSlotViewCreated.OnNext(slotView);
            }
        }
    }
}
