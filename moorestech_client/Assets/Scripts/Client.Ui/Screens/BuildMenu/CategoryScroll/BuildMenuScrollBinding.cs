using Client.Ui.Components.Controls;
using Client.Ui.Screens.BuildMenu.Parts;
using UniRx;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// 表示ごとに視口を張替え、隠すとき位置を保存して凍結
    /// Swaps in a fresh viewport per showing, saves and freezes it on hide, and applies the requested spacer height to the list
    /// </summary>
    public sealed class BuildMenuScrollBinding
    {
        private readonly GameScrollView _scroll;
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private BuildMenuScrollViewport _viewport;

        public BuildMenuScrollBinding(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker)
        {
            _scroll = scroll;
            _list = list;
            _tracker = tracker;
            BuildMenuScrollClipInset.Apply(scroll);

            // スペーサ要求は表示に依らず当てる（D-9）
            // Apply spacer requests to the list whether or not the menu is shown (D-9)
            tracker.SpacerHeight.Subscribe(list.SetSpacerHeight);
        }

        public void Attach()
        {
            // 新しい視口へ要求済みのスペーサ高を当て直してから張る（L-4）
            // Re-apply the already requested spacer height before attaching the new viewport (L-4)
            _list.SetSpacerHeight(_tracker.SpacerHeight.Value);
            _viewport = new BuildMenuScrollViewport(_scroll, _list, _tracker);
            _tracker.AttachViewport(_viewport);
        }

        public void Detach()
        {
            // silent-ok: 一度も出していなければ凍らせる視口が無い / 次の Attach が新しい視口を張る
            // silent-ok: nothing to freeze if never shown / the next Attach installs a fresh viewport
            if (_viewport == null) return;
            _tracker.PersistScrollPosition();
            _viewport.Freeze();
        }
    }
}
