using System;
using System.Collections.Generic;
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

            // 基盤Master（依存なし）
            // Base Masters (no dependencies)
            ItemMaster = new ItemMaster(GetJson(masterJsonFileContainer, new JsonFileName("items")));
            InitializeMaster(ItemMaster);

            FluidMaster = new FluidMaster(GetJson(masterJsonFileContainer, new JsonFileName("fluids")));
            InitializeMaster(FluidMaster);

            CharacterMaster = new CharacterMaster(GetJson(masterJsonFileContainer, new JsonFileName("characters")));
            InitializeMaster(CharacterMaster);

            // ItemMaster, FluidMaster依存
            // Depends on ItemMaster, FluidMaster
            BlockMaster = new BlockMaster(GetJson(masterJsonFileContainer, new JsonFileName("blocks")));
            InitializeMaster(BlockMaster);

            // ItemMaster依存
            // Depends on ItemMaster
            CraftRecipeMaster = new CraftRecipeMaster(GetJson(masterJsonFileContainer, new JsonFileName("craftRecipes")));
            InitializeMaster(CraftRecipeMaster);

            MapObjectMaster = new MapObjectMaster(GetJson(masterJsonFileContainer, new JsonFileName("mapObjects")));
            InitializeMaster(MapObjectMaster);

            PlaceSystemMaster = new PlaceSystemMaster(GetJson(masterJsonFileContainer, new JsonFileName("placeSystem")));
            InitializeMaster(PlaceSystemMaster);

            TrainUnitMaster = new TrainUnitMaster(GetJson(masterJsonFileContainer, new JsonFileName("train")));
            InitializeMaster(TrainUnitMaster);

            // BlockMaster, ItemMaster, FluidMaster依存
            // Depends on BlockMaster, ItemMaster, FluidMaster
            MachineRecipesMaster = new MachineRecipesMaster(GetJson(masterJsonFileContainer, new JsonFileName("machineRecipes")));
            InitializeMaster(MachineRecipesMaster);

            // 複数依存
            // Multiple dependencies
            ChallengeMaster = new ChallengeMaster(GetJson(masterJsonFileContainer, new JsonFileName("challenges")));
            InitializeMaster(ChallengeMaster);

            ResearchMaster = new ResearchMaster(GetJson(masterJsonFileContainer, new JsonFileName("research")));
            InitializeMaster(ResearchMaster);

            #region Internal

            void InitializeMaster(IMasterValidator validator)
            {
                // バリデーション実行
                // Execute validation
                if (!validator.Validate(out var errorLogs))
                {
                    throw new InvalidOperationException($"Master data validation failed:\n{errorLogs}");
                }

                // 初期化処理実行
                // Execute initialization
                validator.Initialize();
            }

            #endregion
        }
        
        private static JToken GetJson(MasterJsonFileContainer masterJsonFileContainer, JsonFileName jsonFileName)
        {
            var index = 0; // TODO 現状はとりあえず一つのmodのみロードする。今後は複数のjsonファイルをロードできるようにする。
            var jsonContent = masterJsonFileContainer.ConfigJsons[index].JsonContents[jsonFileName];
            
            return (JToken)JsonConvert.DeserializeObject(jsonContent);
        }
    }
}