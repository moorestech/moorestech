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
using Mooresmaster;
using UnityEngine;

namespace Core.Master
{
    public class MasterHolder
    {
        public static ItemMaster ItemMaster { get; private set; }
        public static BlockMaster BlockMaster { get; private set; }
        public static ChallengeMaster ChallengeMaster { get; private set; }
        public static CraftRecipeMaster CraftRecipeMaster { get; private set; }
        public static MachineRecipesMaster MachineRecipesMaster { get; private set; }
        public static MapObjectMaster MapObjectMaster { get; private set; }
        
        public static void Load(MasterJsonFileContainer masterJsonFileContainer)
        {
            ItemMaster = new ItemMaster(GetJson(masterJsonFileContainer, new JsonFileName("items")));
            BlockMaster = new BlockMaster(GetJson(masterJsonFileContainer, new JsonFileName("blocks")), ItemMaster);
            ChallengeMaster = new ChallengeMaster(GetJson(masterJsonFileContainer, new JsonFileName("challenges")));
            
            CraftRecipeMaster = new CraftRecipeMaster(GetJson(masterJsonFileContainer, new JsonFileName("craftRecipes")));
            
            MachineRecipesMaster = new MachineRecipesMaster(GetJson(masterJsonFileContainer, new JsonFileName("machineRecipes")));
            MapObjectMaster = new MapObjectMaster(GetJson(masterJsonFileContainer, new JsonFileName("mapObjects")));
        }
        
        private static JToken GetJson(MasterJsonFileContainer masterJsonFileContainer,JsonFileName jsonFileName)
        {
            var index = 0; // TODO 現状はとりあえず一つのmodのみロードする。今後は複数のjsonファイルをロードできるようにする。
            var jsonContent = masterJsonFileContainer.ConfigJsons[index].JsonContents[jsonFileName];
            
            return (JToken)JsonConvert.DeserializeObject(jsonContent);
        }
    }
}