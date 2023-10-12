using Newtonsoft.Json;

namespace Mod.Loader
{
    [JsonObject("ModMeta")]
    public class ModMetaJson
    {
        [JsonProperty("author")] private string _modAuthor;
        [JsonProperty("description")] private string _modDescription;


        [JsonProperty("id")] private string _modId;
        [JsonProperty("name")] private string _modName;
        [JsonProperty("version")] private string _modVersion;

        /// <summary>
        ///     内部的にmodIdは 製作者名 + : + modId として扱う
        /// </summary>
        public string ModId => _modAuthor + ":" + _modId;

        public string ModName => _modName;
        public string ModVersion => _modVersion;
        public string ModAuthor => _modAuthor;
        public string ModDescription => _modDescription;
    }
}