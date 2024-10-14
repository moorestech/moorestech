using MessagePack;

namespace Game.Gear.Common
{
    [MessagePackObject]
    public class GearStateData
    {
        public const string BlockStateDetailKey = "GearStateData";
        
        public GearStateData(float currentRpm, bool isClockwise)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
        }
        
        [Key(0)] public float CurrentRpm { get; set; }
        
        [Key(1)] public bool IsClockwise { get; set; }
    }
}