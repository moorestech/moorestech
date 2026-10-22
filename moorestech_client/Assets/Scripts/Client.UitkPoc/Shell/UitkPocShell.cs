using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Shell
{
    // タブ列とデモ表示領域。非表示のデモは木から外し、計測を表示中の1枚に限る
    // Tab strip and demo host; inactive demos are detached so measurements cover only the visible one
    public class UitkPocShell
    {
        public readonly VisualElement Root;
        public readonly FrameTimeMeter Meter = new();
        private readonly VisualElement _contentHost;
        private readonly VisualElement _tabStrip;
        private readonly List<IUitkPocDemo> _demos;
        private readonly Dictionary<IUitkPocDemo, VisualElement> _createdViews = new();
        private readonly Dictionary<IUitkPocDemo, Button> _tabs = new();

        private int _lastElementCount;

        public IUitkPocDemo ActiveDemo { get; private set; }

        public UitkPocShell(List<IUitkPocDemo> demos)
        {
            _demos = demos;
            Root = new VisualElement();
            Root.AddToClassList("poc-root");

            // 上段にタブ列と計測バッジ、下に表示領域
            // Tab strip with meter on top, demo host below
            var header = new VisualElement();
            header.AddToClassList("poc-header");
            _tabStrip = new VisualElement();
            _tabStrip.AddToClassList("poc-tab-strip");
            header.Add(_tabStrip);
            header.Add(Meter.Badge);
            Root.Add(header);

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("poc-content");
            Root.Add(_contentHost);

            foreach (var demo in _demos) CreateTab(demo);

            #region Internal

            void CreateTab(IUitkPocDemo demo)
            {
                var tab = new Button(() => Show(demo)) { text = demo.Title };
                tab.AddToClassList("poc-tab");
                _tabStrip.Add(tab);
                _tabs[demo] = tab;
            }

            #endregion
        }

        public void Show(IUitkPocDemo demo)
        {
            if (ActiveDemo != null)
            {
                _createdViews[ActiveDemo].RemoveFromHierarchy();
                _tabs[ActiveDemo].RemoveFromClassList("poc-tab--active");
            }

            // 初回だけビューを作り、以後は使い回す
            // Build the view once and reuse it afterwards
            if (!_createdViews.TryGetValue(demo, out var view))
            {
                view = demo.CreateView();
                view.AddToClassList("poc-demo-view");
                _createdViews[demo] = view;
            }
            _contentHost.Add(view);
            _tabs[demo].AddToClassList("poc-tab--active");
            ActiveDemo = demo;
        }

        public void ShowByTitle(string title)
        {
            foreach (var demo in _demos)
            {
                if (demo.Title == title) Show(demo);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (Meter.AddFrame(deltaSeconds, _lastElementCount)) _lastElementCount = CountActiveElements();
        }

        private int CountActiveElements()
        {
            return ActiveDemo == null ? 0 : CountRecursive(_createdViews[ActiveDemo]);

            #region Internal

            int CountRecursive(VisualElement element)
            {
                var count = 1;
                foreach (var child in element.hierarchy.Children()) count += CountRecursive(child);
                return count;
            }

            #endregion
        }
    }
}
