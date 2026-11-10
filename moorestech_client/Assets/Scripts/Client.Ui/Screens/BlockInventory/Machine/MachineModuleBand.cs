using System.Collections.Generic;
using System.Globalization;
using Client.Localization;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Machine.Slots;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// モジュールの帯
    /// Module band (Web MachineInventoryBody): label → per-slot limit → module slots, one level below the process row
    /// </summary>
    public sealed class MachineModuleBand : VisualElement
    {
        private readonly LocalizedLabel _stackLimit = new() { name = "machine-module-stack-limit" };
        private readonly MachineSlotBand _slots;

        public MachineModuleBand(GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster)
        {
            // UiStack は子の marginTop を gap で上書きするので、Web の mt=xs は器の内側の余白で写す
            // UiStack overwrites a child's marginTop with the gap, so the Web mt=xs is ported as this box's inner padding
            pickingMode = PickingMode.Ignore;
            AddToClassList("machine-module-band-wrap");
            var stack = new UiStack(UiGap.Px4) { name = "machine-module-band", pickingMode = PickingMode.Ignore };
            stack.AddToClassList(UiLayoutClass.StackAlignCenter);

            var label = new LocalizedLabel { name = "machine-module-label", pickingMode = PickingMode.Ignore };
            label.AddToClassList(UiTypographyClass.TextSm);
            label.AddToClassList(UiTypographyClass.TextDimmed);
            label.SetKey(LocalizationKeys.Ui.BlockInventory.UpgradeSlots);
            _stackLimit.pickingMode = PickingMode.Ignore;
            _stackLimit.AddToClassList(UiTypographyClass.TextXs);
            _stackLimit.AddToClassList(UiTypographyClass.TextDimmed);
            _slots = new MachineSlotBand("machine-module-slots", "machine-module-slot", commands, toaster, content);

            stack.AddItem(label);
            stack.AddItem(_stackLimit);
            stack.AddItem(_slots.Grid);
            Add(stack);

            // 機械でないブロックでも載るので、文の無いラベルが LogError にならないよう文を先に入れる
            // This is mounted on non-machine blocks too, so seed the text to keep the text-less label from logging an error
            RenderStackLimit(0);
        }

        public void Render(MachineSectionViewModel viewModel)
        {
            // モジュールの枠は束縛なし
            // Module slots carry no binding (no ghost)
            var bound = new List<BoundItemSlot>(viewModel.Bands.Module.Count);
            foreach (var index in viewModel.Bands.Module) bound.Add(BoundItemSlot.WithoutGhost(index));
            _slots.Render(bound, viewModel.ItemSlots, false);
            RenderStackLimit(viewModel.ModuleStackLimit);
        }

        private void RenderStackLimit(int limit)
        {
            _stackLimit.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ModuleSlotStackLimit, new[] { new LocalizationTextParam("limit", limit.ToString(CultureInfo.InvariantCulture)) }));
        }
    }
}
