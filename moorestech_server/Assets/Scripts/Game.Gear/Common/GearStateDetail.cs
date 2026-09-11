using System;
using MessagePack;

namespace Game.Gear.Common
{
    [MessagePackObject]
    public class GearStateDetail
    {
        public const string BlockStateDetailKey = "GearStateData";

        [Key(0)] public bool IsClockwise { get; set; }
        [Key(1)] public float CurrentRpm { get; set; }
        [Key(2)] public float CurrentTorque { get; set; }

        // 役割はサーバーが決めて送る。クライアントは再導出しない
        // The server settles and sends the role; the client never re-derives it
        [Key(3)] public GearRole Role { get; set; }

        public GearStateDetail(bool isClockwise, float currentRpm, float currentTorque, GearRole role)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
            CurrentTorque = currentTorque;
            Role = role;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearStateDetail()
        {
        }
    }
}
