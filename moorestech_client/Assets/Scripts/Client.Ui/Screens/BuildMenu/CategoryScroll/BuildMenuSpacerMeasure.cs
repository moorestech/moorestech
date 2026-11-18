namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// 末尾スペーサの実測の結果。Changed は View の反映待ちを表す
    /// The trailing-spacer measurement; Changed means waiting for the view to apply it
    /// </summary>
    internal enum BuildMenuSpacerMeasure
    {
        Unmeasurable,
        Changed,
        Settled,
    }
}
