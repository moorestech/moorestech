using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public static FluidMaster FluidMaster { get; private set; }
        public static CharacterMaster CharacterMaster { get; private set; }
        public static ResearchMaster ResearchMaster { get; private set; }
        public static PlaceSystemMaster PlaceSystemMaster { get; private set; }
        public static TrainUnitMaster TrainUnitMaster { get; private set; }

        public static void Load(MasterJsonFileContainer masterJsonFileContainer)
        {
            // ロード順序は依存関係に基づいて決定
            // Load order is determined based on dependencies
            ItemMaster = new ItemMaster(GetJson(masterJsonFileContainer, new JsonFileName("items")));
            FluidMaster = new FluidMaster(GetJson(masterJsonFileContainer, new JsonFileName("fluids")));
            BlockMaster = new BlockMaster(GetJson(masterJsonFileContainer, new JsonFileName("blocks")), ItemMaster);

            CraftRecipeMaster = new CraftRecipeMaster(GetJson(masterJsonFileContainer, new JsonFileName("craftRecipes")));

            MachineRecipesMaster = new MachineRecipesMaster(GetJson(masterJsonFileContainer, new JsonFileName("machineRecipes")));
            MapObjectMaster = new MapObjectMaster(GetJson(masterJsonFileContainer, new JsonFileName("mapObjects")));
            CharacterMaster = new CharacterMaster(GetJson(masterJsonFileContainer, new JsonFileName("characters")));

            // ChallengeMasterとResearchMasterはGameActionでCraftRecipeMasterに依存
            // ChallengeMaster and ResearchMaster depend on CraftRecipeMaster for GameAction validation
            ChallengeMaster = new ChallengeMaster(GetJson(masterJsonFileContainer, new JsonFileName("challenges")));
            ResearchMaster = new ResearchMaster(GetJson(masterJsonFileContainer, new JsonFileName("research")));

            PlaceSystemMaster = new PlaceSystemMaster(GetJson(masterJsonFileContainer, new JsonFileName("placeSystem")));
            TrainUnitMaster = new TrainUnitMaster(GetJson(masterJsonFileContainer, new JsonFileName("train")), ItemMaster);
        }
        
        private static JToken GetJson(MasterJsonFileContainer masterJsonFileContainer, JsonFileName jsonFileName)
        {
            var index = 0; // TODO 現状はとりあえず一つのmodのみロードする。今後は複数のjsonファイルをロードできるようにする。
            var jsonContent = masterJsonFileContainer.ConfigJsons[index].JsonContents[jsonFileName];
            
            return (JToken)JsonConvert.DeserializeObject(jsonContent);
        }
    }
}