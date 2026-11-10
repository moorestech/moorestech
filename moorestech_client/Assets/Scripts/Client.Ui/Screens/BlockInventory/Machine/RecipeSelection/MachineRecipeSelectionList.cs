using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.Recipe.Data;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// 機械のレシピ選択モードの本体
    /// Recipe-selection mode body (Web MachineRecipeSelectionList): rows in a scroller led by the context, presses streamed as GUIDs
    /// </summary>
    public sealed class MachineRecipeSelectionList : VisualElement
    {
        public const string ListTestid = "machine-recipe-selection";

        private readonly RecipeListScrollView _scroll = new(ListTestid);
        private readonly GameContentPresentation _content;
        private readonly IUiOsCursor _osCursor;
        private readonly Subject<Guid> _rowPressed = new();
        private readonly List<ShownRow> _rows = new();
        private readonly CompositeDisposable _rowSubscriptions = new();
        private VisualElement _shownLeading;

        public IObservable<Guid> OnRowPressed => _rowPressed;

        public MachineRecipeSelectionList(GameContentPresentation content, IUiOsCursor osCursor)
        {
            _content = content;
            _osCursor = osCursor;
            pickingMode = PickingMode.Ignore;
            AddToClassList("machine-recipe-selection-list");
            Add(_scroll);
        }

        public void Render(MachineSectionViewModel viewModel, VisualElement leading)
        {
            // 先頭の中身は前回と違うときだけ付け替える
            // Re-attach the leading content only when it differs from last time
            if (_shownLeading != leading)
            {
                _scroll.SetLeadingContent(leading);
                _shownLeading = leading;
            }

            // 並びが同じなら行を残して選択の印だけ当て直す（毎 tick の状態更新で ClickEvent とスクロールを失わない。D-37）
            // Same order keeps the rows and only reapplies the selection mark (per-tick updates must not drop ClickEvent or scroll; D-37)
            if (IsSameOrder(viewModel))
            {
                for (var i = 0; i < _rows.Count; i++) _rows[i].View.SetSelected(viewModel.Rows[i].Selected);
                return;
            }
            Rebuild(viewModel);

            #region Internal

            void Rebuild(MachineSectionViewModel viewModel)
            {
                // 購読を捨て、行を作り直して押下を集約
                // Drop subscriptions, rebuild rows, merge presses
                _rowSubscriptions.Clear();
                _rows.Clear();
                var entries = new List<VisualElement>(viewModel.Rows.Count);
                foreach (var row in viewModel.Rows)
                {
                    // 行は解放済みのカタログから組まれるので必ず引ける。引けなければ組み立ての誤りとして知らせ、その行を出さない
                    // Rows are built from the unlocked catalog so the lookup always succeeds; a miss is a build bug, reported, and the row is omitted
                    if (!viewModel.TryGetRecipe(row.Recipe.RecipeGuid, out var recipe))
                    {
                        Debug.LogError($"[MachineRecipeSelectionList] recipe {row.Recipe.RecipeGuid} of a selection row is missing from the catalog; the row is not shown");
                        continue;
                    }
                    var view = new MachineRecipeSelectionRowView(row, recipe, _content, _osCursor);
                    view.OnPressed.Subscribe(_rowPressed.OnNext).AddTo(_rowSubscriptions);
                    _rows.Add(new ShownRow(row.Recipe.RecipeGuid, recipe, view));
                    entries.Add(view);
                }
                _scroll.SetEntries(entries);
            }

            bool IsSameOrder(MachineSectionViewModel viewModel)
            {
                // GUID とレシピの値が同じ並びか
                // Same order of both GUIDs and recipe values; a rebuilt catalog yields new values
                if (viewModel.Rows.Count != _rows.Count) return false;
                for (var i = 0; i < _rows.Count; i++)
                {
                    var guid = viewModel.Rows[i].Recipe.RecipeGuid;
                    if (_rows[i].RecipeGuid != guid) return false;
                    if (!viewModel.TryGetRecipe(guid, out var recipe) || !ReferenceEquals(_rows[i].Recipe, recipe)) return false;
                }
                return true;
            }

            #endregion
        }

        public void ClearLeadingContent()
        {
            // 覚えた先頭も外す（R-14）
            // Forget the remembered leading too, else re-entering selection skips prepending it (R-14)
            _scroll.ClearLeadingContent();
            _shownLeading = null;
        }

        private readonly struct ShownRow
        {
            public readonly Guid RecipeGuid;
            public readonly MachineRecipeData Recipe;
            public readonly MachineRecipeSelectionRowView View;

            public ShownRow(Guid recipeGuid, MachineRecipeData recipe, MachineRecipeSelectionRowView view)
            {
                RecipeGuid = recipeGuid;
                Recipe = recipe;
                View = view;
            }
        }
    }
}
