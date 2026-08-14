namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     カーソル解放と回転可否は常に逆相1状態のためenum契約で表す
    ///     Cursor freedom and rotatability form one inverse-paired state, expressed as an enum contract
    /// </summary>
    public enum CameraInteractionMode
    {
        PointerFree,
        CameraLook,
    }
}
