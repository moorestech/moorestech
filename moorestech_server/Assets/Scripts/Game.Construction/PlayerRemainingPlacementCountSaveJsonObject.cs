using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Construction
{
    public class PlayerRemainingPlacementCountSaveJsonObject
    {
        [JsonProperty("PlayerId")] public int PlayerId;
        [JsonProperty("Entries")] public List<RemainingPlacementCountEntrySaveJsonObject> Entries;

        public PlayerRemainingPlacementCountSaveJsonObject()
        {
        }

        internal PlayerRemainingPlacementCountSaveJsonObject(int playerId, List<RemainingPlacementCountEntrySaveJsonObject> entries)
        {
            PlayerId = playerId;
            Entries = entries;
        }
    }
}
