using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Recipe.Screen;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// エントリを GUID で使い回す列（Web の React key と同じく、描き直しで長押しの状態を失わない）
    /// List that reuses entries by GUID (like the Web React key, a redraw never loses the hold state)
    /// </summary>
    public sealed class RecipeEntryList : VisualElement
    {
        private const string ListTestId = "recipe-entry-list";
        private const string ClipInsetProperty = "--recipe-entry-list-clip-inset";
        private const string StackGapProperty = "--recipe-entry-list-stack-gap";

        private readonly RecipeEntryPartsContext _context;
        private readonly RecipeListScrollView _scroll = new(ListTestId);
        private readonly Dictionary<Guid, VisualElement> _viewsByGuid = new();
        private readonly List<Guid> _order = new();

        public RecipeEntryList(RecipeEntryPartsContext context)
        {
            _context = context;
            AddToClassList("recipe-entry-list");
            Add(_scroll);
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyClipClearance());
        }

        public void Render(IReadOnlyList<RecipeEntryViewModel> entries)
        {
            // 並びが変われば GUID の辞書で既存の要素を使い回して並べ直す（解放で増えても長押し中の要素は同じ。D-34）
            // On an order change, reuse existing elements from the GUID map and re-lay them (an unlock keeps the holding element; D-34)
            if (!SameOrder(entries)) Relayout(entries);
            foreach (var entry in entries) Apply(_viewsByGuid[entry.RecipeGuid], entry);

            #region Internal

            bool SameOrder(IReadOnlyList<RecipeEntryViewModel> entries)
            {
                if (entries.Count != _order.Count) return false;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].RecipeGuid != _order[i]) return false;
                }
                return true;
            }

            void Relayout(IReadOnlyList<RecipeEntryViewModel> entries)
            {
                // 残る GUID は既存の要素を、新しい GUID だけ作る
                // Keep elements for surviving GUIDs and create only new ones
                var next = new List<VisualElement>(entries.Count);
                var kept = new HashSet<Guid>();
                foreach (var entry in entries)
                {
                    kept.Add(entry.RecipeGuid);
                    if (!_viewsByGuid.TryGetValue(entry.RecipeGuid, out var view))
                    {
                        view = Create(entry);
                        _viewsByGuid.Add(entry.RecipeGuid, view);
                    }
                    next.Add(view);
                }

                // 消えた GUID の要素は捨てる
                // Drop elements whose GUID is gone
                foreach (var guid in _order)
                {
                    if (!kept.Contains(guid)) _viewsByGuid.Remove(guid);
                }
                _order.Clear();
                foreach (var entry in entries) _order.Add(entry.RecipeGuid);

                // SetEntries（C4-c）は一度外して付け直す。外れている間は長押しのスケジューラが止まり、同じフレームで付け直すので経過は保たれる（D-34）
                // SetEntries (C4-c) detaches and re-attaches; the hold scheduler pauses while detached and resumes in the same frame, so progress is kept (D-34)
                _scroll.SetEntries(next);
            }

            VisualElement Create(RecipeEntryViewModel entry)
            {
                return entry switch
                {
                    CraftRecipeEntryViewModel craft => new CraftRecipeEntryView(craft, _context),
                    MachineRecipeEntryViewModel machine => new MachineRecipeEntryView(machine, _context),
                    _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, "未知のレシピエントリ"),
                };
            }

            static void Apply(VisualElement view, RecipeEntryViewModel entry)
            {
                switch (view)
                {
                    case CraftRecipeEntryView craftView:
                        craftView.Render((CraftRecipeEntryViewModel)entry);
                        break;
                    case MachineRecipeEntryView machineView:
                        machineView.Render((MachineRecipeEntryViewModel)entry);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(view), view, "未知のレシピエントリの要素");
                }
            }

            #endregion
        }

        private void ApplyClipClearance()
        {
            // Web の margin: calc(-1 * clip-inset) と視口の padding: clip-inset。USS に calc が無いので C# が当てる（前例 ResearchCalcKnobs）
            // The Web margin: calc(-1 * clip-inset) and viewport padding: clip-inset; USS has no calc, so C# applies them (precedent ResearchCalcKnobs)
            // 読めないときは UssCustomStyleReader が LogError を出し、余白は前の値のまま
            // When unreadable UssCustomStyleReader logs an error and the spacing keeps its previous value
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ClipInsetProperty, out var inset)) return;
            if (!UssCustomStyleReader.TryReadPixels(customStyle, StackGapProperty, out var gap)) return;

            // 上は見出しとの Stack gap sm を足す（Web は Stack の margin-top gap と打ち消しの -inset の和）
            // The top adds the Stack gap sm below the header (the Web sums the Stack margin-top gap and the cancelling -inset)
            style.marginTop = gap - inset;
            style.marginRight = -inset;
            style.marginBottom = -inset;
            style.marginLeft = -inset;

            // 余白は中身の側に置き、スクロールの範囲に下の逃げも入れる（視口に置くと最下段の下の逃げが範囲から落ちる）
            // Pad the content, not the viewport, so the scroll range includes the bottom clearance (viewport padding drops it from the range)
            var content = _scroll.Q<GameScrollView>().contentContainer;
            content.style.paddingTop = inset;
            content.style.paddingRight = inset;
            content.style.paddingBottom = inset;
            content.style.paddingLeft = inset;
        }
    }
}
