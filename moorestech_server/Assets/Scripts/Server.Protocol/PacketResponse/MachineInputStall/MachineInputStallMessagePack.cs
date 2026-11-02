using System;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class MachineInputStallMessagePack
    {
        [Key(0)] public MachineInputStallReasonKind Reason { get; set; }
        [Key(1)] public int WaitingItemId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MachineInputStallMessagePack() { }

        public MachineInputStallMessagePack(MachineInputStallReasonKind reason, int waitingItemId)
        {
            Reason = reason;
            WaitingItemId = waitingItemId;
        }
    }

    // wire の理由。サーバーの MachineInputLevelRefusalReason はプロトコルの switch 1か所でここへ写す
    // The wire reason; the server's MachineInputLevelRefusalReason maps onto it in a single switch in the protocol
    public enum MachineInputStallReasonKind
    {
        MixedLevel,
        AboveCraftCap,
    }

    // 応答の判別子
    // The response discriminator
    public enum MachineInputStallResponseKind
    {
        BlockNotFound,
        NotStalled,
        Stalled,
        UnsupportedTarget,
    }
}
