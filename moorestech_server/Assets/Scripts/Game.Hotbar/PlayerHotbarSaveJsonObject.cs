using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Hotbar
{
    public class PlayerHotbarSaveJsonObject
    {
        [JsonProperty("PlayerId")] public int PlayerId;

        // 9枠Guid文字列(未割当はEmpty)
        // 9 GUID strings, one per slot; unassigned slots hold Guid.Empty's string form
        [JsonProperty("Assignments")] public List<string> Assignments;

        public PlayerHotbarSaveJsonObject()
        {
        }

        public PlayerHotbarSaveJsonObject(int playerId, List<string> assignments)
        {
            PlayerId = playerId;
            Assignments = assignments;
        }
    }
}
