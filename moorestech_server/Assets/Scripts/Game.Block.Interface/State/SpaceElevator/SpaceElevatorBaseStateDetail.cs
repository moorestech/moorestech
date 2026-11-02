using System;
using MessagePack;

namespace Game.Block.Interface.State
{
    [Serializable]
    [MessagePackObject]
    public class SpaceElevatorBaseStateDetail
    {
        public const string BlockStateDetailKey = "SpaceElevatorBase";

        [Key(0)] public bool IsConnected { get; private set; }
        [Key(1)] public int CablePoolAmount { get; private set; }
        // 昇降の進み（0〜1）。昇っていなければ0
        // Climb progress from 0 to 1; zero while not climbing
        [Key(2)] public float ClimbProgress { get; private set; }
        [Key(3)] public SpaceElevatorBaseStatus Status { get; private set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SpaceElevatorBaseStateDetail()
        {
        }

        public SpaceElevatorBaseStateDetail(bool isConnected, int cablePoolAmount, float climbProgress, SpaceElevatorBaseStatus status)
        {
            IsConnected = isConnected;
            CablePoolAmount = cablePoolAmount;
            ClimbProgress = climbProgress;
            Status = status;
        }
    }
}
