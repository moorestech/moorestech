using System;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// マス操作の受け口（コマンド呼出しと sticky は View）
    /// Receiver for slot actions; the view owns the command calls and the sticky detail
    /// </summary>
    public interface IBuildMenuEntryHandler
    {
        void OnEntrySelected(Guid targetId);
        void OnBlueprintDeleteRequested(Guid targetId);
        void OnEntryHovered(Guid targetId);
    }
}
