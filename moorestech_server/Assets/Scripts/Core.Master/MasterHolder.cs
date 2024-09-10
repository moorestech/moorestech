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
        
        public static void Load(MasterJsonFileContainer masterJsonFileContainer)
        {
            Items = ItemsLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("items")));
            ItemMaster.Load();
            
            Blocks = BlocksLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("blocks")));
            BlockMaster.Load(); // TODO こういうメソッドは自動で呼べるようにする
            
            Challenges = ChallengesLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("challenges")));
            CraftRecipes = CraftRecipesLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("craftRecipes")));
            
            MachineRecipes = MachineRecipesLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("machineRecipes")));
            MapObjects = MapObjectsLoader.Load(GetJson(masterJsonFileContainer, new JsonFileName("mapObjects")));
        }
        
        private static JToken GetJson(MasterJsonFileContainer masterJsonFileContainer,JsonFileName jsonFileName)
        {
            var index = 0; // TODO 現状はとりあえず一つのmodのみロードする。今後は複数のjsonファイルをロードできるようにする。
            var jsonContent = masterJsonFileContainer.ConfigJsons[index].JsonContents[jsonFileName];
            
            return (JToken)JsonConvert.DeserializeObject(jsonContent);
        }
    }
}