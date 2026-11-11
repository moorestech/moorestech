using System;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// マスの操作を受ける口。コマンドの呼び出しと詳細の sticky は View が持つ
    /// Receiver for slot actions; the view owns the command calls and the sticky detail
    /// </summary>
    public interface IBuildMenuEntryHandler
    {
        void OnEntrySelected(Guid targetId);
        void OnBlueprintDeleteRequested(Guid targetId);
        void OnEntryHovered(Guid targetId);
    }
}
