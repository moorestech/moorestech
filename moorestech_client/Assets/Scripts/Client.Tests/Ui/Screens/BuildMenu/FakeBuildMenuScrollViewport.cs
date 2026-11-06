using System;
using System.Collections.Generic;
using Client.Ui.Screens.BuildMenu.CategoryScroll;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    /// <summary>
    /// 寸法と位置をテストから動かせる視口。スムーズスクロールの要求は記録だけする
    /// A viewport whose sizes and position tests can drive; smooth-scroll requests are only recorded
    /// </summary>
    public sealed class FakeBuildMenuScrollViewport : IBuildMenuScrollViewport
    {
        public static readonly Guid A = new("00000000-0000-4000-8000-00000000000a");
        public static readonly Guid B = new("00000000-0000-4000-8000-00000000000b");
        public static readonly Guid X = new("00000000-0000-4000-8000-0000000000f1");
        public static readonly Guid Y = new("00000000-0000-4000-8000-0000000000f2");
        public static readonly Guid Z = new("00000000-0000-4000-8000-0000000000f3");

        public readonly List<float> SmoothScrollRequests = new();
        private readonly Dictionary<Guid, float> _headingTops = new();
        private readonly Dictionary<Guid, float> _groupHeights = new();

        // View が反映済みのスペーサ高さ。追跡器の SpacerHeight とは別に、テストが ApplySpacer で明示的に反映する
        // The spacer height the view has applied; separate from the tracker's SpacerHeight, tests apply it explicitly via ApplySpacer
        private float _contentHeight;

        public float ScrollTop { get; private set; }
        public float ClientHeight { get; private set; }
        public float AppliedSpacerHeight { get; private set; }
        public float ScrollHeight => _contentHeight + AppliedSpacerHeight;

        public FakeBuildMenuScrollViewport(float clientHeight, float scrollHeight, float scrollTop)
        {
            ClientHeight = clientHeight;
            _contentHeight = scrollHeight;
            ScrollTop = scrollTop;
        }

        public void SetScrollTop(float scrollTop) => ScrollTop = scrollTop;
        public void SetClientHeight(float clientHeight) => ClientHeight = clientHeight;
        public void SetHeadingTop(Guid categoryGuid, float top) => _headingTops[categoryGuid] = top;
        public void SetGroupHeight(Guid categoryGuid, float height) => _groupHeights[categoryGuid] = height;

        public void SetContentHeight(float contentHeight) => _contentHeight = contentHeight;
        public void ApplySpacer(float spacerHeight) => AppliedSpacerHeight = spacerHeight;

        public bool TryGetHeadingTop(Guid categoryGuid, out float top) => _headingTops.TryGetValue(categoryGuid, out top);
        public bool TryGetGroupHeight(Guid categoryGuid, out float height) => _groupHeights.TryGetValue(categoryGuid, out height);
        public void SetScrollTopImmediately(float scrollTop) => ScrollTop = Math.Max(0f, Math.Min(scrollTop, ScrollHeight - ClientHeight));
        public void ScrollSmoothlyTo(float scrollTop) => SmoothScrollRequests.Add(scrollTop);
    }
}
