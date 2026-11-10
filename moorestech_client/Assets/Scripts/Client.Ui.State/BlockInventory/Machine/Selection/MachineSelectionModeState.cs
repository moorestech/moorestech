using System;
using Client.Ui.State.BlockInventory.Machine.Detail;

namespace Client.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 選択モードの局所状態。作り直しで消える
    /// Local selection-mode state; it resets when the section is rebuilt, like Web useState
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
            // 押した GUID を要求として覚える
            // Remember the pressed GUID as the request; the mode closes once the server's selection matches it
            _hasRequest = true;
            _requestedRecipeGuid = recipeGuid;
        }

        public bool ShowsSelection(MachineSectionViewModel viewModel)
        {
            // 選べるレシピが無い機械は選択モードなし
            // A machine without selectable recipes has no selection mode
            if (viewModel.Rows.Count == 0) return false;
            if (viewModel.Detail.Selection is UnselectedMachineRecipe) return true;
            if (!viewModel.TryGetSelectedRow(out _)) return true;

            // 要求がサーバーの選択に一致するまで開く
            // Stay open until the requested recipe matches the server's selection; a rejection keeps it open (C14)
            var requestApplied = _hasRequest && viewModel.Detail.Selection.Matches(_requestedRecipeGuid);
            return _selectionOpened && !requestApplied;
        }
    }
}
