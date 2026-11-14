using System.Collections.Generic;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 子へ --ui-gap を余白で配る
    /// Hands --ui-gap to children as margins
    /// </summary>
    /// <remarks>
    /// 余白はインラインで当てる。部品が 2 クラスで当てる margin: 0（PanelActionButton）に負けないため
    /// The space is applied inline so it beats the two-class margin: 0 of components such as PanelActionButton
    /// 表示切替は SetItemShown（規則は UiDisplay.SetShown と同じで、間隔も当て直す）
    /// Toggle children with SetItemShown (same rule as UiDisplay.SetShown, and it re-applies the spacing)
    /// 継承の Add/Insert で入れた子は間隔の対象外
    /// Children added via the inherited Add/Insert are outside the spacing
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
            // 再追加は末尾へ移すので、登録も末尾へ移して実際の並びと揃える
            // Re-adding moves the child to the end, so move its entry too to match the real order
            _items.Remove(item);
            _items.Add(item);
            ItemParent.Add(item);
            ApplyLeadingSpaces();
        }

        public void SetItemShown(VisualElement item, bool shown)
        {
            UiDisplay.SetShown(item, shown);
            ApplyLeadingSpaces();
        }

        public void ClearItems()
        {
            _items.Clear();
            ItemParent.Clear();
        }

        // 子を載せる要素。既定は自分自身
        // The element that holds the children; defaults to this
        protected virtual VisualElement ItemParent => this;

        protected virtual void OnGapPixelsResolved(float pixels)
        {
        }

        protected abstract void ApplyItemSpace(VisualElement item, bool isFirstShown, float gapPixels);

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出す。間隔は前の値のまま
            // When unreadable, UssCustomStyleReader logs an error and the previous gap stays
            if (!UssCustomStyleReader.TryReadPixels(customStyle, GapProperty, out var pixels)) return;
            _gapPixels = pixels;
            _gapResolved = true;
            OnGapPixelsResolved(pixels);
            ApplyLeadingSpaces();
        }

        private void ApplyLeadingSpaces()
        {
            // silent-ok: スタイル解決の前は間隔が未定 / パネルに載ると CustomStyleResolvedEvent が必ず来て置き直す（L1 UiLayoutTest）
            if (!_gapResolved) return;

            // Remove などで外された子は数えない
            // Children detached by Remove and the like are not counted
            _items.RemoveAll(item => item.parent != ItemParent);

            // 余白の当て方は派生が決める。表示中の先頭かどうかだけ渡す
            // The subclass decides how to space; only whether the child is the first shown is passed
            var foundFirstShown = false;
            foreach (var item in _items)
            {
                if (!UiDisplay.IsShownInline(item)) continue;
                ApplyItemSpace(item, !foundFirstShown, _gapPixels);
                foundFirstShown = true;
            }
        }
    }
}
