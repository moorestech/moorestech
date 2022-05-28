using Newtonsoft.Json;

namespace Server.StartServerSystem.Mod
{
    [JsonObject("ModMeta")]
    public class ModMetaJson
    {
        public string ModId => _modId;
        public string ModName => _modName;
        public string ModVersion => _modVersion;
        public string ModAuthor => _modAuthor;
        public string ModDescription => _modDescription;


        [JsonProperty("mod_id")]private string _modId;
        [JsonProperty("mod_name")]private string _modName;
        [JsonProperty("mod_version")]private string _modVersion;
        [JsonProperty("mod_author")]private string _modAuthor;
        [JsonProperty("mod_description")]private string _modDescription;
    }
}