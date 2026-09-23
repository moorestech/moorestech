using System;
using Newtonsoft.Json;
namespace Game.Block.Blocks.BeltConveyor
{
    public sealed class BeltCellSaveState
    {
        [JsonProperty(Required = Required.Always)] public readonly int PriorityIndex;
        [JsonProperty(Required = Required.AllowNull)] public readonly BeltSavedItem RunningItem, BufferedItem;
        [JsonConstructor]
        public BeltCellSaveState(int priorityIndex, BeltSavedItem runningItem, BeltSavedItem bufferedItem)
        {
            if (priorityIndex < 0 || 2 < priorityIndex) throw new ArgumentOutOfRangeException(nameof(priorityIndex));
            PriorityIndex = priorityIndex; RunningItem = runningItem; BufferedItem = bufferedItem;
        }
    }
}
