using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 呼ばれた操作を文字列で記録する
    /// Records called operations as strings
    /// </summary>
    public sealed class RecordingHotbarCommands : IHotbarCommands
    {
        public readonly List<string> Calls = new();

        public UiCommandResult Select(int slotIndex)
        {
            Calls.Add($"select:{slotIndex}");
            return UiCommandAccepted.Instance;
        }

        public void Drop(HotbarDragSource source, HotbarDropTarget target)
        {
            Calls.Add($"{SourceText(source)}->{TargetText(target)}");
        }

        private static string SourceText(HotbarDragSource source)
        {
            return source switch
            {
                BuildMenuEntryDragSource entry => $"entry:{entry.TargetId:D}",
                HotbarSlotDragSource slot => $"slot:{slot.SlotIndex}",
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
            };
        }

        private static string TargetText(HotbarDropTarget target)
        {
            return target switch
            {
                HotbarSlotDropTarget slot => $"slot:{slot.SlotIndex}",
                OutsideHotbarDropTarget => "outside",
                HotbarRowGapDropTarget => "gap",
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
            };
        }
    }
}
