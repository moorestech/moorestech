using System;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Recipe.Screen;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// エントリを GUID で使い回す列（並びが同じ描き直しは長押しが続き、並べ直しで木から外れたら止まる。D-51）
    /// List that reuses entries by GUID (a same-order redraw keeps the hold; a reorder detaches and stops it, D-51)
    /// </summary>
    public sealed class RecipeEntryList : VisualElement
    {
        private const string ListTestId = "recipe-entry-list";
        private const string ClipInsetProperty = "--recipe-entry-list-clip-inset";
        private const string StackGapProperty = "--recipe-entry-list-stack-gap";

        private readonly RecipeEntryPartsContext _context;
        private readonly RecipeListScrollView _scroll = new(ListTestId);
        private readonly Dictionary<Guid, CraftRecipeEntryView> _craftViewsByGuid = new();
        private readonly Dictionary<Guid, MachineRecipeEntryView> _machineViewsByGuid = new();
        private readonly List<VisualElement> _order = new();

        public RecipeEntryList(RecipeEntryPartsContext context)
        {
            _context = context;
            AddToClassList("recipe-entry-list");
            Add(_scroll);
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyClipClearance());
        }

        public void Render(IReadOnlyList<RecipeEntryViewModel> entries)
        {
            // 種類ごとの GUID の辞書で既存の要素を引き、新しいものだけ作る（解放で増えても要素は同じ。長押しは外れで止まる）
            // Look up existing elements in the per-kind GUID maps and create only new ones (an unlock keeps the element; the hold stops on detach)
            var next = new List<VisualElement>(entries.Count);
            foreach (var entry in entries) next.Add(Resolve(entry));

            // 並びが変わったときだけ付け直し、そのあと全エントリへ VM を描く
            // Re-lay only when the order changed, then render every entry's VM
            if (!SameOrder(next)) Relayout(next);
            foreach (var entry in entries) Apply(entry);

            #region Internal

            VisualElement Resolve(RecipeEntryViewModel entry)
            {
                switch (entry)
                {
                    case CraftRecipeEntryViewModel craft:
                        if (!_craftViewsByGuid.TryGetValue(craft.RecipeGuid, out var craftView))
                        {
                            craftView = new CraftRecipeEntryView(craft, _context);
                            _craftViewsByGuid.Add(craft.RecipeGuid, craftView);
                        }
                        return craftView;
                    case MachineRecipeEntryViewModel machine:
                        if (!_machineViewsByGuid.TryGetValue(machine.RecipeGuid, out var machineView))
                        {
                            machineView = new MachineRecipeEntryView(machine, _context);
                            _machineViewsByGuid.Add(machine.RecipeGuid, machineView);
                        }
                        return machineView;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(entry), entry, "未知のレシピエントリ");
                }
            }

            bool SameOrder(List<VisualElement> next)
            {
                if (next.Count != _order.Count) return false;
                for (var i = 0; i < next.Count; i++)
                {
                    if (next[i] != _order[i]) return false;
                }
                return true;
            }

            void Relayout(List<VisualElement> next)
            {
                // 消えたエントリの要素は辞書から捨てる
                // Drop elements of entries that are gone from the maps
                var kept = new HashSet<VisualElement>(next);
                RemoveDropped(_craftViewsByGuid, kept);
                RemoveDropped(_machineViewsByGuid, kept);
                _order.Clear();
                _order.AddRange(next);

                // SetEntries（C4-c）は一度外して付け直す。外れで長押しは止まり押し直しが要る（捕捉が外れ離しが届かないため。D-34 を覆す判断記録）
                // SetEntries (C4-c) detaches and re-attaches; the detach stops the hold and a re-press is needed (capture is lost so no release arrives)
                _scroll.SetEntries(next);
            }

            void Apply(RecipeEntryViewModel entry)
            {
                switch (entry)
                {
                    case CraftRecipeEntryViewModel craft:
                        _craftViewsByGuid[craft.RecipeGuid].Render(craft);
                        break;
                    case MachineRecipeEntryViewModel machine:
                        _machineViewsByGuid[machine.RecipeGuid].Render(machine);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(entry), entry, "未知のレシピエントリ");
                }
            }

            static void RemoveDropped<TView>(Dictionary<Guid, TView> viewsByGuid, HashSet<VisualElement> kept) where TView : VisualElement
            {
                var dropped = new List<Guid>();
                foreach (var pair in viewsByGuid)
                {
                    if (!kept.Contains(pair.Value)) dropped.Add(pair.Key);
                }
                foreach (var guid in dropped) viewsByGuid.Remove(guid);
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
