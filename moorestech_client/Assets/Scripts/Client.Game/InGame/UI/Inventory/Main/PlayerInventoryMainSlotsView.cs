// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     スロット数に応じ動的生成
    ///     Dynamically creates views by slot count
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
            // 縮小（レベルダウン）は仕様上発生しない
            // Shrinking never happens by design
            while (_slotViews.Count < slotCount)
            {
                var slotView = Instantiate(itemSlotViewPrefab, slotsParent);
                _slotViews.Add(slotView);
                _onSlotViewCreated.OnNext(slotView);
            }
        }
    }
}
