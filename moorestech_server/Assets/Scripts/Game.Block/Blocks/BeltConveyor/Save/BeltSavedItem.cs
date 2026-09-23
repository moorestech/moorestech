using System;
using Game.BeltSegment;
using Newtonsoft.Json;
namespace Game.Block.Blocks.BeltConveyor
{
    public sealed class BeltSavedItem
    {
        [JsonProperty(Required = Required.Always)] public readonly Guid TransportGuid, ItemMasterGuid;
        [JsonProperty(Required = Required.Always)] public readonly int Progress;
        [JsonProperty(Required = Required.Always)] public readonly BeltDirection Entry;
        [JsonConstructor]
        public BeltSavedItem(Guid transportGuid, Guid itemMasterGuid, int progress, BeltDirection entry)
        {
            if (transportGuid == Guid.Empty || itemMasterGuid == Guid.Empty || progress < 1 || BeltConstants.ItemWidth < progress || (int)entry < 0 || 3 < (int)entry)
                throw new ArgumentException("Invalid saved belt item.");
            TransportGuid = transportGuid; ItemMasterGuid = itemMasterGuid; Progress = progress; Entry = entry;
        }
    }
}
