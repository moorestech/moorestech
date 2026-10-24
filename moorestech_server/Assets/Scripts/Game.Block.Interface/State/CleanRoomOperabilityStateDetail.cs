using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     クリーンルーム機械の稼働可否（停止理由の表示用）
    ///     Clean-room machine operability, used to show the halt reason
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class CleanRoomOperabilityStateDetail
    {
        public const string BlockStateDetailKey = "CleanRoomOperability";

        [Key(0)] public CleanRoomOperability Operability;

        public CleanRoomOperabilityStateDetail(CleanRoomOperability operability)
        {
            Operability = operability;
        }

        public static BlockStateDetail CreateState(CleanRoomOperability operability)
        {
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(new CleanRoomOperabilityStateDetail(operability)));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CleanRoomOperabilityStateDetail() { }
    }
}
