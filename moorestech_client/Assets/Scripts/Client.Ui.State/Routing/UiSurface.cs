namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 画面上で所有者を切り替える描画面
    /// A drawing surface whose owner can be switched per screen
    /// </summary>
    public enum UiSurface
    {
        MainPanel,
    }

    /// <summary>
    /// 描画面を描く担当
    /// Who draws a surface
    /// </summary>
    public enum UiSurfaceOwner
    {
        Web,
        GameUi,
    }
}
