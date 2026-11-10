using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// レシピ行のスクローラ（先頭内容＋列）
    /// Recipe row scroller (leading content and list)
    /// </summary>
    public sealed class RecipeListScrollView : VisualElement
    {
        public const string UssClassName = "recipe-list-scroll-view";
        private const string BleedProperty = "--recipe-list-scroll-view-bleed";
        private const string ReserveProperty = "--recipe-list-scroll-view-reserve";

        private readonly VisualElement _leading;
        private readonly UiStack _list;

        public RecipeListScrollView(string listTestid)
        {
            AddToClassList(UssClassName);

            // 先頭の内容と列を 1 つのスクロールに入れる（Web の ScrollArea の子 2 つ）
            // Leading content and the list share one scroll (the Web ScrollArea's two children)
            var scroll = new GameScrollView($"{listTestid}-scroll");
            scroll.AddToClassList("recipe-list-scroll-view__scroll");
            Add(scroll);
            _leading = new VisualElement();
            _leading.AddToClassList("recipe-list-scroll-view__leading");
            scroll.Add(_leading);
            // 行の列は Mantine の Stack の写し UiStack。間隔は USS で --ui-gap を --recipe-list-gap へ上書きする
            // The row list is UiStack (the Mantine Stack); USS overrides --ui-gap with --recipe-list-gap
            _list = new UiStack(UiGap.None) { name = listTestid };
            _list.AddToClassList("recipe-list-scroll-view__list");
            scroll.Add(_list);
            _list.RegisterCallback<CustomStyleResolvedEvent>(_ => OnListStyleResolved());
        }

        public void SetLeadingContent(VisualElement content)
        {
            _leading.Clear();
            _leading.Add(content);
        }

        public void ClearLeadingContent()
        {
            _leading.Clear();
        }

        public void SetEntries(IReadOnlyList<VisualElement> entries)
        {
            _list.ClearItems();
            foreach (var entry in entries) _list.AddItem(entry);
        }

        private void OnListStyleResolved()
        {
            // 右の余白は Web の calc(bleed + reserve)。USS に calc が無いので 2 つのつまみから C# が当てる（A-47）
            // The right padding is the Web calc(bleed + reserve); USS has no calc, so C# derives it from the two knobs (A-47)
            // 読めないときは UssCustomStyleReader が LogError を出し、右は USS の bleed のまま
            // When unreadable, UssCustomStyleReader logs an error and the right keeps the USS bleed
            if (UssCustomStyleReader.TryReadPixels(_list.customStyle, BleedProperty, out var bleed) && UssCustomStyleReader.TryReadPixels(_list.customStyle, ReserveProperty, out var reserve))
            {
                _list.style.paddingRight = bleed + reserve;
            }
        }
    }
}
