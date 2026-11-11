using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 呼ばれた操作を文字列で記録する
    /// Records the called operations as strings
    /// </summary>
    internal sealed class RecordingHotbarCommands : IHotbarCommands
    {
        public readonly List<string> Calls = new();

        public UiCommandResult Select(int slotIndex)
        {
            Calls.Add($"select:{slotIndex}");
            return UiCommandAccepted.Instance;
        }

        public void Apply(HotbarDropAction action)
        {
            Calls.Add(action switch
            {
                HotbarAssignAction assign => $"assign:{assign.SlotIndex}:{assign.TargetId:D}",
                HotbarSwapAction swap => $"swap:{swap.FromSlotIndex}:{swap.ToSlotIndex}",
                HotbarClearAction clear => $"clear:{clear.SlotIndex}",
                HotbarNoDropAction => "none",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
            });
        }
    }
}
