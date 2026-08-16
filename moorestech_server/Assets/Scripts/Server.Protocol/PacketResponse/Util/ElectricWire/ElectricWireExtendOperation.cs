namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// 電線延長要求が指示する操作種別。既存接続・新設電柱への延長・孤立設置の3択
    /// Operation requested by an electric wire extend request: connect existing, extend to a new pole, or isolated place
    /// </summary>
    public enum ElectricWireExtendOperation
    {
        ConnectToExisting,
        ExtendToNewPole,
        PlaceIsolatedPole,
    }
}
