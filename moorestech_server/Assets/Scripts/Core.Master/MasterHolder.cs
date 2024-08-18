using System;
using System.IO;
using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Loader.CraftRecipesModule;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Loader.MapObjectsModule;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.CraftRecipesModule;
using Mooresmaster.Model.ItemsModule;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.MapObjectsModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MasterHolder
    {
        public static Items Items { get; private set; }
        public static Blocks Blocks { get; private set; }
        
        public static Challenges Challenges { get; private set; }
        public static CraftRecipes CraftRecipes { get; private set; }
        public static MachineRecipes MachineRecipes { get; private set; }
        public static MapObjects MapObjects { get; private set; }
        
        public static void Load(ConfigJsonFileContainer configJsonFileContainer)
        {
            Items = ItemsLoader.Load(GetJson(modDirectory, modName, "items"));
            Blocks = BlocksLoader.Load(GetJson(modDirectory, modName, "blocks"));
            
            Challenges = ChallengesLoader.Load(GetJson(modDirectory, modName, "challenges"));
            CraftRecipes = CraftRecipesLoader.Load(GetJson(modDirectory, modName, "craftRecipes"));
            
            MachineRecipes = MachineRecipesLoader.Load(GetJson(modDirectory, modName, "machineRecipes"));
            MapObjects = MapObjectsLoader.Load(GetJson(modDirectory, modName, "mapObjects"));
        }
        
        private static JToken GetJson(string modDirectory, string modName,string jsonName)
        {
            var blockJsonPath = Path.Combine(modDirectory, modName, $"{jsonName}.json");
            var blockJson = File.ReadAllText(blockJsonPath);
            return (JToken)JsonConvert.DeserializeObject(blockJson);
        }
    }
}