using Core.Master;
using MessagePack;

namespace Game.Fluid
{
    [MessagePackObject]
    public class FluidStateDetail
    {
        public const string FluidStateDetailKey = "FluidStateData";
        
        public FluidStateDetail(FluidId fluidId, float amount, float capacity)
        {
            FluidId = fluidId;
            Amount = amount;
            Capacity = capacity;
        }
        
        [Key(0)] public FluidId FluidId { get; set; }
        [Key(1)] public float Amount { get; set; }
        [Key(2)] public float Capacity { get; set; }
    }
}