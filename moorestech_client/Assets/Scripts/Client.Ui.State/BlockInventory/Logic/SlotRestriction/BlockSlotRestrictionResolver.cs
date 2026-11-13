using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.ItemMove;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.SlotRestriction
{
    /// <summary>
    /// Shift 配分の宛先候補を種別ごとに決める（Web blockSlotRestriction.ts の写し）。ホストが配る束縛が正本で、規則はここで導き直さない
    /// Settles Shift-allocation candidates per block type (port of Web blockSlotRestriction.ts); host-published bindings are the source of truth
    /// </summary>
    public static class BlockSlotRestrictionResolver
    {
        public static BlockSlotRestriction Resolve(BlockSlotRestrictionInput block, ItemId itemId)
        {
            // 取得失敗は候補探索の前に返す
            // Propagate fetch failure before looking up candidate slots
            if (block is OpenFailedBlockSlotInput) return BlockSlotRestriction.InventoryOpenFailed;
            var open = (OpenBlockSlotInput)block;

            // 入れ口固定の種別は出力枠へ入れない
            // Fixed-inlet types never insert into their output slots
            return open.IsFixedInletBlock ? BoundByInletBindings(open) : ResolveMachine(open);

            #region Internal

            // 束縛の未着は取得待ちとして確定の拒否と分ける
            // Keep pending bindings distinct from a confirmed refusal
            BlockSlotRestriction BoundByInletBindings(OpenBlockSlotInput fixedInletBlock)
            {
                if (!(fixedInletBlock.Detail is InletBlockSlotDetail inlet)) return BlockSlotRestriction.DetailLoading;
                var candidates = new List<int>();
                foreach (var binding in inlet.InletBindings)
                {
                    if (AcceptedItems.Contains(binding.AcceptedItemIds, itemId)) candidates.Add(binding.Slot);
                }
                return BlockSlotRestriction.Bound(candidates);
            }

            // 機械は入力帯だけ束縛で絞り、出力帯とモジュール帯は候補に残す。機械以外（チェスト等）は無制限
            // Machines narrow only the input band; output and module bands stay candidates. Non-machines (chests etc.) are unrestricted
            // 詳細の未着も無制限にする（Web の !block.machine と同じ。サーバーが束縛外を拒み既存の拒否通知が出る。判断記録 D-17）
            // A pending detail is unrestricted too (same as Web !block.machine; the server refuses unbound slots with the existing notice, D-17)
            BlockSlotRestriction ResolveMachine(OpenBlockSlotInput machineBlock)
            {
                if (!(machineBlock.Detail is MachineBlockSlotDetail detail) || detail.Machine.SlotBindings.Count == 0) return BlockSlotRestriction.Unrestricted;
                var bands = detail.Machine.Layout.Split(machineBlock.ItemSlotCount);
                var candidates = MachineSlotGhosts.BoundMachineInputSlotsForItem(detail.Machine, machineBlock.ItemSlotCount, itemId);
                candidates.AddRange(bands.Output);
                candidates.AddRange(bands.Module);
                return BlockSlotRestriction.Bound(candidates);
            }

            #endregion
        }
    }
}
