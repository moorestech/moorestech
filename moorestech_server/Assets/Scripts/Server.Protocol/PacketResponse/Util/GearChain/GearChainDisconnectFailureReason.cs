namespace Server.Protocol.PacketResponse.Util.GearChain
{
    // 拒否通知の接尾辞にも使う失敗理由
    // Failure reasons also used as denial notification suffixes
    public enum GearChainDisconnectFailureReason
    {
        None,
        InvalidTarget,
        NotConnected,
        InventoryFull,
    }
}
