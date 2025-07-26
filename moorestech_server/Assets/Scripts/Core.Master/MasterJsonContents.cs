using System.Collections.Generic;
using UnitGenerator;

namespace Core.Master
{
    public class MasterJsonContents
    {
        public readonly ModId ModId;
        
        /// <summary>
        /// Key : json file name ( Do not include ".json" )
        /// Value : json file contents
        /// </summary>
        public readonly Dictionary<JsonFileName,string> JsonContents = new();
        
        
        public MasterJsonContents(ModId modId, Dictionary<JsonFileName,string> jsonContents)
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