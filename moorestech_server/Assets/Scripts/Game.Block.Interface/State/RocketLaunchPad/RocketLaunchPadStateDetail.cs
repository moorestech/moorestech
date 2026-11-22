using System;
using Core.Master;
using MessagePack;
using Game.Block.Interface.Component;

namespace Game.Block.Interface.State
{
    // 段階・待ち理由・進捗・選んだ積荷・今の便の積荷をクライアントへ
    // Send phase, wait reason, progress, the selected cargo and the current flight's cargo to the client
    [MessagePackObject]
    public class RocketLaunchPadStateDetail
    {
        public const string BlockStateDetailKey = "RocketLaunchPad";

        [Key(0)] public RocketLaunchPadPhase Phase { get; set; }
        [Key(1)] public RocketLaunchPadWaitReason WaitReason { get; set; }
        [Key(2)] public float Progress { get; set; }
        // 次の便から積む積荷。未選択なら null
        // The cargo loaded from the next flight; null while unselected
        [Key(3)] public ItemId? SelectedCargoItemId { get; }
        // 建造中〜打ち上げ待ちの便が積んでいる積荷。積んだ便が無ければ null。選び直した後は SelectedCargoItemId と違う値になる
        // The payload of the flight from building to awaiting launch; null without one, and it differs from SelectedCargoItemId after a reselection
        [Key(4)] public ItemId? LoadedCargoItemId { get; }

        [SerializationConstructor]
        public RocketLaunchPadStateDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, ItemId? selectedCargoItemId, ItemId? loadedCargoItemId)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
            SelectedCargoItemId = selectedCargoItemId;
            LoadedCargoItemId = loadedCargoItemId;
        }

        public static BlockStateDetail CreateState(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, ItemId? selectedCargoItemId, ItemId? loadedCargoItemId)
        {
            var stateDetail = new RocketLaunchPadStateDetail(phase, waitReason, progress, selectedCargoItemId, loadedCargoItemId);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RocketLaunchPadStateDetail() { }
    }
}
