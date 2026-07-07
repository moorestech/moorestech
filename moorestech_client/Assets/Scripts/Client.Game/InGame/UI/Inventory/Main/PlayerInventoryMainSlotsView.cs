using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using Game.PlayerInventory.Interface;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     スロット数に応じ動的生成
    ///     Dynamically creates views by slot count
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
            // 初回にホットバー登録
            // Register hotbar slots first
            if (_slotViews.Count == 0)
            {
                // 参照漏れは末尾インデックス計算を壊すため明示的に失敗させる
                // A missing reference breaks tail index math, so fail explicitly
                if (hotBarSlotViews.Count != PlayerInventoryConst.HotBarSlotCount) throw new Exception($"hotBarSlotViews は {PlayerInventoryConst.HotBarSlotCount} 件必要です (現在 {hotBarSlotViews.Count} 件)");

                foreach (var hotBarSlotView in hotBarSlotViews)
                {
                    _slotViews.Add(hotBarSlotView);
                    _onSlotViewCreated.OnNext(hotBarSlotView);
                }
            }

            // 縮小（レベルダウン）は仕様上発生しない
            // Shrinking never happens by design
            while (_slotViews.Count < slotCount)
            {
                var slotView = Instantiate(itemSlotViewPrefab, slotsParent);
                _slotViews.Insert(_slotViews.Count - hotBarSlotViews.Count, slotView);
                _onSlotViewCreated.OnNext(slotView);
            }
        }
    }
}
