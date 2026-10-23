using System;
using MessagePack;
using Game.Block.Interface.Component;

namespace Game.Block.Interface.State
{
    // 発射台の段階・待ち理由・進み具合をクライアントへ送る
    // Sends the launch pad's phase, wait reason and progress to the client
    [MessagePackObject]
    public class RocketLaunchPadStateDetail
    {
        public const string BlockStateDetailKey = "RocketLaunchPad";

        [Key(0)] public RocketLaunchPadPhase Phase { get; set; }
        [Key(1)] public RocketLaunchPadWaitReason WaitReason { get; set; }
        [Key(2)] public float Progress { get; set; }

        public RocketLaunchPadStateDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
        }

        public static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress)
        {
            var stateDetail = new RocketLaunchPadStateDetail(phase, waitReason, progress);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RocketLaunchPadStateDetail() { }
    }
}
