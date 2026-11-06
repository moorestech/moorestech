using System;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// Mantine の Stack/Group の gap の段。Web の gap={2}/{4} は px、xs〜xl は --mantine-spacing-*
    /// Gap steps of Mantine Stack/Group; Web gap={2}/{4} are px and xs..xl are --mantine-spacing-*
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
        // Layout.uss のクラス名の末尾（ui-stack--gap-<末尾>）
        // Class-name suffix in Layout.uss (ui-stack--gap-<suffix>)
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
