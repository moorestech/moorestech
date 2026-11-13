using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Grab;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作を判定して送り結果を写す
    /// Checks, sends special-family commands and maps outcomes
    /// </summary>
    public sealed class SpecialBlockCommands : ISpecialBlockCommands
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ISpecialBlockRequestSender _sender;
        private readonly PlayerGrabModel _grab;

        public SpecialBlockCommands(IBlockInventoryPanelSource panels, ISpecialBlockRequestSender sender, PlayerGrabModel grab)
        {
            _panels = panels;
            _sender = sender;
            _grab = grab;
        }

        public async UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.FilterSplitter, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SetFilterMode(block.Position, block.BlockInstanceId, directionIndex, mode));
        }

        public async UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.FilterSplitter, out var block, out var rejected)) return rejected;

            // 掴みが空の左押下は送らない
            // A left press with an empty grab sends nothing
            var grab = _grab.Current.Value;
            var action = FilterSplitterLogic.ClickAction(grab.Count, clear);
            if (action == FilterSlotClickAction.Noop) return UiCommandRejected.Of(UiCommandRejectReason.FilterSplitterGrabEmpty);
            var itemId = action == FilterSlotClickAction.Clear ? ItemMaster.EmptyItemId : grab.ItemId;
            return ToResult(await _sender.SetFilterItem(block.Position, block.BlockInstanceId, directionIndex, slotIndex, itemId));
        }

        public async UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SetElectricToGearMode(block.Position, modeIndex));
        }

        public async UniTask<UiCommandResult> ConnectSpaceElevator()
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.ConnectSpaceElevator(block.Position, block.BlockInstanceId));
        }

        public async UniTask<UiCommandResult> SendRocketCommand(RocketCommand command)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.RocketLaunchPad, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SendRocketCommand(block.Position, command, block.BlockInstanceId));
        }

        // 未オープン・種別違いは送らず拒否
        // Refuse without sending when not open or type differs
        private bool TryOpen(string expectedBlockType, out OpenBlockInventoryPanel block, out UiCommandResult rejected)
        {
            rejected = null;
            block = null;
            if (_panels.Current.Value is not OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open)
            {
                rejected = UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
                return false;
            }
            block = open;
            if (block.BlockType == expectedBlockType) return true;
            rejected = UiCommandRejected.OverridingWireCode(UiCommandRejectReason.BlockNotOpen, "invalid_block_type");
            return false;
        }

        private static UiCommandResult ToResult(SpecialBlockRequestOutcome outcome)
        {
            return outcome.Kind switch
            {
                SpecialBlockRequestOutcomeKind.Accepted => UiCommandAccepted.Instance,
                SpecialBlockRequestOutcomeKind.Stale => UiCommandRejected.OverridingWireCode(UiCommandRejectReason.SpecialBlockCommandStale, outcome.WireCode),
                _ => UiCommandRejected.OverridingWireCode(UiCommandRejectReason.SpecialBlockRequestFailed, outcome.WireCode),
            };
        }
    }
}
