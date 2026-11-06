using System.Collections.Generic;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 表示中の先頭以外の子へ USS の --ui-gap を余白として配る並びの土台（USS に gap が無いため）
    /// Base of a row/column that hands USS --ui-gap to every shown child except the first (USS has no gap)
    /// </summary>
    /// <remarks>
    /// 余白はインラインで当てる。部品が 2 クラスで当てる margin: 0（PanelActionButton）に負けないため
    /// The space is applied inline so it beats the two-class margin: 0 of components such as PanelActionButton
    /// 子の表示切替は SetItemShown で行う。USS で隠した子は数えたまま残る
    /// Toggle children with SetItemShown; a child hidden by USS is still counted
    /// </remarks>
    public abstract class UiGapContainer : VisualElement
    {
        private const string GapProperty = "--ui-gap";
        private readonly string _rootClass;
        private readonly List<VisualElement> _items = new();
        private string _gapClass;
        private float _gapPixels;
        private bool _gapResolved;

        protected UiGapContainer(string rootClass, UiGap gap)
        {
            _rootClass = rootClass;
            AddToClassList(rootClass);
            SetGap(gap);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        public void SetGap(UiGap gap)
        {
            if (_gapClass != null) RemoveFromClassList(_gapClass);
            _gapClass = $"{_rootClass}--gap-{UiGapClass.Suffix(gap)}";
            AddToClassList(_gapClass);
        }

        public void AddItem(VisualElement item)
        {
            _items.Add(item);
            Add(item);
            ApplyLeadingSpaces();
        }

        public void SetItemShown(VisualElement item, bool shown)
        {
            item.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyLeadingSpaces();
        }

        public void ClearItems()
        {
            _items.Clear();
            Clear();
        }

        protected abstract void SetLeadingSpace(VisualElement item, StyleLength space);

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出す。間隔は前の値のまま
            // When unreadable, UssCustomStyleReader logs an error and the previous gap stays
            if (!UssCustomStyleReader.TryReadPixels(customStyle, GapProperty, out var pixels)) return;
            _gapPixels = pixels;
            _gapResolved = true;
            ApplyLeadingSpaces();
        }

        private void ApplyLeadingSpaces()
        {
            // silent-ok: スタイル解決の前は間隔が未定 / パネルに載ると CustomStyleResolvedEvent が必ず来て置き直す（L1 UiLayoutTest）
            if (!_gapResolved) return;

            // 表示中の先頭は USS の余白に任せ、それ以降に gap を当てる
            // The first shown child keeps its USS margin; later shown children get the gap
            var foundFirstShown = false;
            foreach (var item in _items)
            {
                if (item.style.display == DisplayStyle.None) continue;
                SetLeadingSpace(item, foundFirstShown ? new StyleLength(_gapPixels) : new StyleLength(StyleKeyword.Null));
                foundFirstShown = true;
            }
        }
    }
}
