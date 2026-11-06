using System;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// Stack/Group の gap の段
    /// Gap steps of Stack/Group
    /// </summary>
    public enum UiGap
    {
        None,
        Px2,
        Px4,
        Xs,
        Sm,
        Md,
        Lg,
        Xl,
    }

    public static class UiGapClass
    {
        // Layout.uss クラス名の末尾
        // Class-name suffix in Layout.uss
        public static string Suffix(UiGap gap)
        {
            return gap switch
            {
                UiGap.None => "none",
                UiGap.Px2 => "2",
                UiGap.Px4 => "4",
                UiGap.Xs => "xs",
                UiGap.Sm => "sm",
                UiGap.Md => "md",
                UiGap.Lg => "lg",
                UiGap.Xl => "xl",
                _ => throw new ArgumentOutOfRangeException(nameof(gap), gap, null),
            };
        }
    }
}
