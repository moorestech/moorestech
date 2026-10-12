namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     接続線の種別。切断・引き直しの送信先を決める
    ///     Connection line kind, deciding where disconnect and restore requests go
    /// </summary>
    public enum ConnectionLineKind
    {
        ElectricWire,
        GearChain,
    }
}
