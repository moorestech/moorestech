using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// 開いているブロックの状態と寄与から L0-s4b の束縛の入力を組む（Web resolveBlockSlotRestriction の入力側）
    /// Builds the L0-s4b binding input from the open block and contributions (input side of Web resolveBlockSlotRestriction)
    /// </summary>
    public sealed class BlockSlotRestrictionInputBuilder
    {
        private readonly IReadOnlyList<IBlockSlotRestrictionContributor> _contributors;

        public BlockSlotRestrictionInputBuilder(IReadOnlyList<IBlockSlotRestrictionContributor> contributors)
        {
            _contributors = contributors;
        }

        public BlockSlotRestrictionInput Build(OpenBlockInventoryPanel panel)
        {
            // 取得失敗は候補を探す前に返す。束縛の未着（寄与なし）は L0-s4b が種別ごとに取得待ちか無制限へ倒す
            // Return fetch failure before candidates; a missing binding (no contribution) falls to pending or unrestricted per type in L0-s4b
            if (panel.Error != BlockInventoryPanelError.None) return BlockSlotRestrictionInput.OpenFailedBlock;
            var input = BlockSlotRestrictionInput.OpenBlock(panel.BlockType, panel.ItemSlots.Count);
            foreach (var contributor in _contributors)
            {
                if (contributor.AppliesTo(panel)) input = contributor.Contribute(panel, input);
            }
            return input;
        }
    }
}
