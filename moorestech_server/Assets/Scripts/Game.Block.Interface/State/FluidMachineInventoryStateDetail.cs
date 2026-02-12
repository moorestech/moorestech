using System;
using System.Collections.Generic;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     機械の液体インベントリのステートの詳細なデータ
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class FluidMachineInventoryStateDetail
    {
        public const string BlockStateDetailKey = "FluidMachineInventory";
        
        /// <summary>
        ///     入力タンクの液体情報
        /// </summary>
        [Key(0)] public List<FluidMessagePack> InputTanks;
        
        /// <summary>
        ///     出力タンクの液体情報
        /// </summary>
        [Key(1)] public List<FluidMessagePack> OutputTanks;
        
        public FluidMachineInventoryStateDetail(List<FluidMessagePack> inputTanks, List<FluidMessagePack> outputTanks)
        {
            InputTanks = inputTanks;
            OutputTanks = outputTanks;
        }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public FluidMachineInventoryStateDetail()
        {
        }
    }
    
    [MessagePackObject]
    public class FluidMessagePack
    {
        [Key(0)] public int FluidId { get; set; }
        [Key(1)] public double Amount { get; set; }
        [Key(2)] public double MaxCapacity { get; set; }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public FluidMessagePack()
        {
        }
        
        public FluidMessagePack(FluidId fluidId, double amount, double maxCapacity)
        {
            FluidId = fluidId.AsPrimitive();
            Amount = amount;
            MaxCapacity = maxCapacity;
        }
        
        public FluidMessagePack(int fluidId, double amount, double maxCapacity)
        {
            FluidId = fluidId;
            Amount = amount;
            MaxCapacity = maxCapacity;
        }
    }
}