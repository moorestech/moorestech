using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 差し込まれた区画の工場を Slot で索引する。二重登録・ItemGrid の外部登録は起動時に止め、空きの Slot は 1 回だけ情報ログに並べる
    /// Indexes plugged section factories by slot; duplicates and an external ItemGrid stop at startup, and empty slots are listed once in an info log
    /// </summary>
    public sealed class SectionStackSlotRegistry
    {
        private readonly Dictionary<SectionStackSlot, ISectionStackSectionFactory> _factories = new();

        public SectionStackSlotRegistry(IReadOnlyList<ISectionStackSectionFactory> factories)
        {
            foreach (var factory in factories)
            {
                if (factory.Slot == SectionStackSlot.ItemGrid) throw new InvalidOperationException($"ItemGrid の区画は S4-alpha の既定。差し込めない: {factory.GetType().Name}");
                if (!_factories.TryAdd(factory.Slot, factory)) throw new InvalidOperationException($"区画の Slot が二重登録: {factory.Slot} {factory.GetType().Name}");
            }

            // 担当レーンが未マージの Slot を 1 回だけ並べる
            // List the slots whose lane is not merged yet, once
            var empty = Enum.GetValues(typeof(SectionStackSlot)).Cast<SectionStackSlot>()
                .Where(slot => slot != SectionStackSlot.ItemGrid && !_factories.ContainsKey(slot));
            Debug.Log($"[BlockInventory] 区画の登録が無い Slot（何も描かない）: {string.Join(", ", empty)}");
        }

        public IBlockInventorySection CreateOrNull(SectionStackSlot slot, SectionStackSectionMount mount)
        {
            // silent-ok: 担当レーンが未マージの区画は Web の null と同じ見え方 / 起動時の情報ログに空きの Slot が並ぶ
            return _factories.TryGetValue(slot, out var factory) ? factory.Create(mount) : null;
        }
    }
}
