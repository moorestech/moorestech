namespace Server.Protocol.PacketResponse
{
    // 応答の判別子。抽選の中身の種別を表す列挙はこれ1つだけ
    // The response discriminator; the only enum naming draw-detail kinds
    public enum CleanRoomYieldResponseKind
    {
        BlockNotFound,
        NoRole,
        NotOperable,
        LayerExposure,
        Dicing,
        UnsupportedTarget,
    }
}
