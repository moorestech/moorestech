using System.Collections.Generic;
using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠の L1 の土台。テストのパネルに印付きの要素を置き、本物の追跡器で測らせる
    /// Base for overlay L1 tests; places marked elements on the test panel and measures them with the real tracker
    /// </summary>
    public abstract class TutorialOverlayTestHost : ScreenTestHost
    {
        protected sealed class FakeTutorialOverlayModel : FakeModel<TutorialOverlayViewModel>
        {
            public FakeTutorialOverlayModel() : base(TutorialOverlayViewModel.Empty) { }
        }

        protected VisualElement MountAnchor(VisualElement parent, string anchorId, float left, float top, float width, float height)
        {
            var anchor = new VisualElement { style = { position = Position.Absolute, left = left, top = top, width = width, height = height } };
            TutorialAnchor.Mark(anchor, anchorId);
            parent.Add(anchor);
            return anchor;
        }

        private readonly List<TutorialAnchorTracker> _trackers = new();

        protected TutorialAnchorTracker CreateTracker()
        {
            var tracker = new TutorialAnchorTracker(rootVisualElement, rootVisualElement);
            _trackers.Add(tracker);
            return tracker;
        }

        [TearDown]
        public void StopTrackingAfterTest()
        {
            // 毎フレームの測定を次のテストへ持ち越さない（前のテストの Warning が次の LogAssert を汚す）
            // Never carry per-frame measuring into the next test (a stale Warning would pollute its LogAssert)
            foreach (var tracker in _trackers) tracker.StopTracking();
            _trackers.Clear();
            // Mount した画面は外れると Presenter が DetachFromPanelEvent で追跡を止める
            // Mounted screens stop their own trackers on DetachFromPanelEvent once removed
            rootVisualElement.Clear();
        }

        protected static float Px(string tokenName)
        {
            return ThemeTokenFile.ResolvePixels(tokenName);
        }

        protected static float Ms(string tokenName)
        {
            // 周期のトークンは "1200ms" の形で書かれている
            // Duration tokens are written like "1200ms"
            return float.Parse(ThemeTokenFile.Resolve(tokenName).Replace("ms", string.Empty), CultureInfo.InvariantCulture);
        }

        protected static Rect UnpulsedWorldBound(VisualElement element)
        {
            // 脈動の拡縮を除いた矩形で見る（worldBound は scale を含み、脈動の位相で揺れる）
            // Read the rect without the pulse scale (worldBound includes scale and sways with the pulse phase)
            return element.parent.LocalToWorld(element.layout);
        }

        protected static IReadOnlyCollection<string> Ids(params string[] anchorIds)
        {
            return anchorIds;
        }
    }
}
