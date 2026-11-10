using System;
using Client.Ui.State.BlockInventory.Machine.Detail;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 機械の区画の選択モードの局所状態。区画が作り直されると消える（Web の useState と同じ寿命）
    /// Local selection-mode state of the machine section; it resets when the section is rebuilt (same lifetime as Web useState)
    /// </summary>
    public sealed class MachineSelectionModeState
    {
        private bool _selectionOpened;
        private bool _hasRequest;
        private Guid _requestedRecipeGuid;

        public void OpenSelection()
        {
            _selectionOpened = true;
            _hasRequest = false;
        }

        public void RememberRequest(Guid recipeGuid)
        {
            // 押した GUID を要求として覚える。サーバーの選択がこれに一致すると閉じる（同じレシピを押し直したときも閉じる）
            // Remember the pressed GUID as the request; the mode closes once the server's selection matches it (also on re-pressing the same recipe)
            _hasRequest = true;
            _requestedRecipeGuid = recipeGuid;
        }

        public bool ShowsSelection(MachineSectionViewModel viewModel)
        {
            // 選べるレシピが無い機械は選択モードを持たない
            // A machine with no selectable recipe has no selection mode
            if (viewModel.Rows.Count == 0) return false;
            if (viewModel.Detail.Selection is UnselectedMachineRecipe) return true;
            if (!viewModel.TryGetSelectedRow(out _)) return true;

            // 要求したレシピがサーバーの選択に一致するまで閉じない（拒否された間は留まる。C14）
            // Stay open until the requested recipe matches the server's selection (a rejection keeps it open, C14)
            var requestApplied = _hasRequest && viewModel.Detail.Selection.Matches(_requestedRecipeGuid);
            return _selectionOpened && !requestApplied;
        }
    }
}
