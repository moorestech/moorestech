using System.Collections.Generic;
using UnitGenerator;

namespace Core.Master
{
    public class MasterJsonCpntens
    {
        public readonly string BlockConfigJson;
        public readonly string ChallengeConfigJson;
        public readonly string CraftRecipeConfigJson;
        public readonly string ItemConfigJson;
        public readonly string MachineRecipeConfigJson;
        public readonly string MapObjectConfigJson;
        
        
        public MasterJsonCpntens(string itemJson, string blockConfigJson, string machineRecipeConfigJson, string craftRecipeConfigJson, string mapObjectConfigJson, string challengeConfigJson,ModId modId, Dictionary<JsonFileName,string> jsonContents)
        {
            ModId = modId;
            JsonContents = jsonContents;
            
            ItemConfigJson = itemJson;
            BlockConfigJson = blockConfigJson;
            MachineRecipeConfigJson = machineRecipeConfigJson;
            CraftRecipeConfigJson = craftRecipeConfigJson;
            MapObjectConfigJson = mapObjectConfigJson;
            ChallengeConfigJson = challengeConfigJson;
        }
        
        public readonly ModId ModId;
        
        /// <summary>
        /// Key : json file name ( Do not include ".json" )
        /// Value : json file contents
        /// </summary>
        public readonly Dictionary<JsonFileName,string> JsonContents = new();
        
        
        public MasterJsonCpntens(ModId modId, Dictionary<JsonFileName,string> jsonContents)
        {
            ModId = modId;
            JsonContents = jsonContents;
        }
    }
    
    [UnitOf(typeof(string))]
    public readonly partial struct ModId { }
    
    [UnitOf(typeof(string))]
    public readonly partial struct JsonFileName { }
}