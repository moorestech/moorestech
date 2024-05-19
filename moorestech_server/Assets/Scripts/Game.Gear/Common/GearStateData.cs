using MessagePack;

namespace Game.Gear.Common
{
    [MessagePackObject]
    public class GearStateData
    {
        [Key(0)]
        public float CurrentRpm { get; set; }
        
        [Key(1)]
        public bool IsClockwise { get; set; }
        
        public GearStateData(float currentRpm, bool isClockwise)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
        }
    }
}