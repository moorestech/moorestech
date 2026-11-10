using Client.Ui.Components.Gauges;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Machine.Slots;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 加工行（Web の processRow）
    /// Process row (Web processRow): input band → progress arrow → output band in three columns, arrow centred
    /// </summary>
    public sealed class MachineProcessRow : VisualElement
    {
        private readonly MachineSlotBand _input;
        private readonly MachineSlotBand _output;
        private readonly ProgressArrowGlyph _arrow = new("machine-progress-arrow");

        public MachineProcessRow(GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster)
        {
            name = "machine-process-row";
            pickingMode = PickingMode.Ignore;
            AddToClassList("machine-process-row");
            _input = new MachineSlotBand("machine-input-slots", "machine-input-slot", commands, toaster, content);
            _output = new MachineSlotBand("machine-output-slots", "machine-output-slot", commands, toaster, content);

            // 左右を等幅で伸ばし矢印を中央に固定
            // Stretch both side boxes equally to pin the arrow at the centre (Web grid 1fr auto 1fr)
            Add(Side("machine-process-row__input", _input));
            Add(_arrow);
            Add(Side("machine-process-row__output", _output));

            #region Internal

            static VisualElement Side(string className, MachineSlotBand band)
            {
                var side = new VisualElement { pickingMode = PickingMode.Ignore };
                side.AddToClassList(className);
                side.Add(band.Grid);
                return side;
            }

            #endregion
        }

        public void Render(MachineSectionViewModel viewModel)
        {
            // 素材の枠だけ所持/必要を出す
            // Only material slots show held/required; outputs never do, even when filled
            _input.Render(viewModel.SlotView.Inputs, viewModel.ItemSlots, true);
            _output.Render(viewModel.SlotView.Outputs, viewModel.ItemSlots, false);
            _arrow.SetProgress(viewModel.Detail.Progress);
        }
    }
}
