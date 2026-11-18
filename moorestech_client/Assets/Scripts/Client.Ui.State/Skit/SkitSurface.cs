namespace Client.Ui.State.Skit
{
    /// <summary>
    /// スキットのどの面を出すか（Web SkitPresentation の早期 return の行き先）
    /// Which skit surface shows (where each early return of the Web SkitPresentation leads)
    /// </summary>
    public enum SkitSurface
    {
        None,
        RestoreOnly,
        ToolbarOnly,
        BackgroundLine,
        Window,
    }
}
