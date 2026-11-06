using System;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// 追跡器が測り・動かす視口。View が ScrollView の上に実装する（DOM の viewport・offsetTop の代わり）
    /// The viewport the tracker measures and moves; the view implements it over a ScrollView (stands in for the DOM viewport and offsetTop)
    /// </summary>
    public interface IBuildMenuScrollViewport
    {
        float ScrollTop { get; }
        float ClientHeight { get; }
        float ScrollHeight { get; }

        // View がスペーサに実際に当てた高さ。追跡器の要求値と一致するまで Settled にしない
        // The height the view actually applied to the spacer; the tracker settles only once it matches the requested value
        float AppliedSpacerHeight { get; }

        // 見出しの上端と群の高さはスクロール内容の座標。未配置なら false
        // Heading tops and group heights are in scroll-content coordinates; false while not laid out
        bool TryGetHeadingTop(Guid categoryGuid, out float top);
        bool TryGetGroupHeight(Guid categoryGuid, out float height);

        void SetScrollTopImmediately(float scrollTop);
        void ScrollSmoothlyTo(float scrollTop);
    }
}
