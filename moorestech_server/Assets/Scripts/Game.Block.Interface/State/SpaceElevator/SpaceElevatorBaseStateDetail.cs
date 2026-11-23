using System;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    [Serializable]
    [MessagePackObject]
    public class SpaceElevatorBaseStateDetail
    {
        public const string BlockStateDetailKey = "SpaceElevatorBase";

        [Key(0)] public bool IsConnected { get; }
        [Key(1)] public int CablePoolAmount { get; }
        // 昇降の進み（0〜1）。昇っていなければ0
        // Climb progress from 0 to 1; zero while not climbing
        [Key(2)] public float ClimbProgress { get; }
        [Key(3)] public SpaceElevatorBaseStatus Status { get; }
        // 次の便から運ぶ積荷。未選択なら null
        // The cargo carried from the next flight; null while unselected
        [Key(4)] public ItemId? SelectedCargoItemId { get; }
        // 昇降中（頂上で出口待ちを含む）の便の積荷。昇っていなければ null。選び直した後は SelectedCargoItemId と違う値になる
        // The payload of the flight being carried (including waiting at the top); null otherwise, and it differs from SelectedCargoItemId after a reselection
        [Key(5)] public ItemId? ClimbingCargoItemId { get; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SpaceElevatorBaseStateDetail()
        {
        }

        [SerializationConstructor]
        public SpaceElevatorBaseStateDetail(bool isConnected, int cablePoolAmount, float climbProgress, SpaceElevatorBaseStatus status, ItemId? selectedCargoItemId, ItemId? climbingCargoItemId)
        {
            IsConnected = isConnected;
            CablePoolAmount = cablePoolAmount;
            ClimbProgress = climbProgress;
            Status = status;
            SelectedCargoItemId = selectedCargoItemId;
            ClimbingCargoItemId = climbingCargoItemId;
        }
    }
}
