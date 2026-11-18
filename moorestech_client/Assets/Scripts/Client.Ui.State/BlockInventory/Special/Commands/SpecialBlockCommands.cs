using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Toast;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作を判定して送り、結果を写してトーストへ渡す
    /// Checks and sends special-family commands, maps outcomes and hands them to the toaster
    /// </summary>
    public sealed class SpecialBlockCommands : ISpecialBlockCommands
    {
        public const string SetFilterModeCommandName = "filter_splitter.set_mode";
        public const string SetFilterItemCommandName = "filter_splitter.set_filter_item";
        public const string SetElectricToGearModeCommandName = "electric_to_gear.set_output_mode";
        public const string ConnectSpaceElevatorCommandName = "space_elevator.connect";
        public const string RocketCommandName = "rocket_launch_pad.command";

        private readonly IBlockInventoryPanelSource _panels;
        private readonly ISpecialBlockRequestSender _sender;
        private readonly PlayerGrabModel _grab;
        private readonly IUiCommandResultToaster _toaster;

        public SpecialBlockCommands(IBlockInventoryPanelSource panels, ISpecialBlockRequestSender sender, PlayerGrabModel grab, IUiCommandResultToaster toaster)
        {
            _panels = panels;
            _sender = sender;
            _grab = grab;
            _toaster = toaster;
        }

        // 各操作は結果を待ってトーストへ渡す（拒否は警告ログ、良性でなければトースト。楽観更新はしない。D-5）
        // Each operation awaits its result and hands it to the toaster (rejects log a warning, non-benign ones toast; no optimistic update. D-5)
        public void SetFilterMode(int directionIndex, FilterSplitterMode mode)
        {
            ReportWhenDone(SetFilterModeCommandName, Send()).Forget();

            #region Internal

            async UniTask<UiCommandResult> Send()
            {
                if (!TryOpen(BlockMasterElement.BlockTypeConst.FilterSplitter, out var block, out var rejected)) return rejected;
                return ToResult(await _sender.SetFilterMode(block.Position, block.BlockInstanceId, directionIndex, mode));
            }

            #endregion
        }

        public void SetFilterItem(int directionIndex, int slotIndex, bool clear)
        {
            ReportWhenDone(SetFilterItemCommandName, Send()).Forget();

            #region Internal

            async UniTask<UiCommandResult> Send()
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

            #endregion
        }

        public void SetElectricToGearMode(int modeIndex)
        {
            ReportWhenDone(SetElectricToGearModeCommandName, Send()).Forget();

            #region Internal

            async UniTask<UiCommandResult> Send()
            {
                if (!TryOpen(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator, out var block, out var rejected)) return rejected;
                return ToResult(await _sender.SetElectricToGearMode(block.Position, modeIndex));
            }

            #endregion
        }

        public void ConnectSpaceElevator()
        {
            ReportWhenDone(ConnectSpaceElevatorCommandName, Send()).Forget();

            #region Internal

            async UniTask<UiCommandResult> Send()
            {
                if (!TryOpen(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, out var block, out var rejected)) return rejected;
                return ToResult(await _sender.ConnectSpaceElevator(block.Position, block.BlockInstanceId));
            }

            #endregion
        }

        public void SendRocketCommand(RocketCommand command)
        {
            ReportWhenDone(RocketCommandName, Send()).Forget();

            #region Internal

            async UniTask<UiCommandResult> Send()
            {
                if (!TryOpen(BlockMasterElement.BlockTypeConst.RocketLaunchPad, out var block, out var rejected)) return rejected;
                return ToResult(await _sender.SendRocketCommand(block.Position, command, block.BlockInstanceId));
            }

            #endregion
        }

        private async UniTaskVoid ReportWhenDone(string commandName, UniTask<UiCommandResult> sending)
        {
            _toaster.Report(commandName, await sending);
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
            rejected = UiCommandRejected.Of(UiCommandRejectReason.BlockTypeMismatch);
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
