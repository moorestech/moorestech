using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.TrainRail
{
    // 貨物プラットフォームのロード/アンロード状態をクライアントへ伝送するためのStateDetail
    // StateDetail used to broadcast the load/unload transfer mode of a train platform to the client
    [MessagePackObject]
    public class TrainPlatformTransferStateDetail
    {
        public const string BlockStateDetailKey = "TrainPlatformTransfer";

        [Key(0)] public TrainPlatformTransferComponent.TransferMode Mode { get; set; }

        public TrainPlatformTransferStateDetail(TrainPlatformTransferComponent.TransferMode mode)
        {
            Mode = mode;
        }

        public static BlockStateDetail CreateState(TrainPlatformTransferComponent.TransferMode mode)
        {
            var stateDetail = new TrainPlatformTransferStateDetail(mode);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public TrainPlatformTransferStateDetail() { }
    }
}
