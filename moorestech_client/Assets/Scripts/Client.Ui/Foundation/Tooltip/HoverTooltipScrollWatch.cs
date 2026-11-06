using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// ポインタの下の対象を含む ScrollView だけのスクロールを知らせる（無関係なパネルのスクロールは見ない。Web の scroll の target.contains(node)）
    /// Reports scrolls only of ScrollViews containing the element under the pointer (unrelated panels are ignored; Web scroll target.contains(node))
    /// </summary>
    internal sealed class HoverTooltipScrollWatch
    {
        private readonly List<Scroller> _watched = new();
        private readonly Subject<Unit> _onAncestorScrolled = new();

        public IObservable<Unit> OnAncestorScrolled => _onAncestorScrolled;

        public void Watch(VisualElement pointed)
        {
            // 対象自身と祖先の ScrollView の縦横のバーを全部見る（入れ子の一覧も含める）
            // Watch both bars of the element itself and every ancestor ScrollView (nested lists included)
            Clear();
            for (var current = pointed; current != null; current = current.parent)
            {
                if (current is not ScrollView scrollView) continue;
                Add(scrollView.verticalScroller);
                Add(scrollView.horizontalScroller);
            }
        }

        public void Clear()
        {
            foreach (var scroller in _watched) scroller.valueChanged -= OnScrolled;
            _watched.Clear();
        }

        private void Add(Scroller scroller)
        {
            scroller.valueChanged += OnScrolled;
            _watched.Add(scroller);
        }

        private void OnScrolled(float offset)
        {
            _onAncestorScrolled.OnNext(Unit.Default);
        }
    }
}
