using System;
using System.Collections.Generic;
using Client.Ui.Screens.BuildMenu.Parts;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// マスの操作を積むだけの受け口
    /// Entry handler that only records the slot actions
    /// </summary>
    internal sealed class RecordingBuildMenuEntryHandler : IBuildMenuEntryHandler
    {
        public List<Guid> Selected { get; } = new();
        public List<Guid> DeleteRequested { get; } = new();
        public List<Guid> Hovered { get; } = new();

        public void OnEntrySelected(Guid targetId)
        {
            Selected.Add(targetId);
        }

        public void OnBlueprintDeleteRequested(Guid targetId)
        {
            DeleteRequested.Add(targetId);
        }

        public void OnEntryHovered(Guid targetId)
        {
            Hovered.Add(targetId);
        }
    }
}
