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
        public static MapVeinMaster MapVeinMaster { get; private set; }
        public static GenerationMaster GenerationMaster { get; private set; }
        public static FluidMaster FluidMaster { get; private set; }
        public static CharacterMaster CharacterMaster { get; private set; }
        public static ResearchMaster ResearchMaster { get; private set; }
        public static TrainUnitMaster TrainUnitMaster { get; private set; }
        public static CleanRoomMaster CleanRoomMaster { get; private set; }
        public static ConnectToolMaster ConnectToolMaster { get; private set; }
        public static BuildMenuCategoryMaster BuildMenuCategoryMaster { get; private set; }

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

            BuildMenuCategoryMaster = new BuildMenuCategoryMaster(GetJson(masterJsonFileContainer, new JsonFileName("buildMenu")));
            InitializeMaster(BuildMenuCategoryMaster);

            // ItemMaster, FluidMaster, BuildMenuCategoryMaster依存（category/subCategoryの参照を検証）
            // Depends on ItemMaster, FluidMaster, BuildMenuCategoryMaster (validates category/subCategory references)
            BlockMaster = new BlockMaster(GetJson(masterJsonFileContainer, new JsonFileName("blocks")));
            InitializeMaster(BlockMaster);

            // ItemMaster依存
            // Depends on ItemMaster
            CraftRecipeMaster = new CraftRecipeMaster(GetJson(masterJsonFileContainer, new JsonFileName("craftRecipes")));
            InitializeMaster(CraftRecipeMaster);

            // ItemMaster依存（requiredItemsのitemGuidを検証）
            // Depends on ItemMaster (validates requiredItems.itemGuid)
            ConnectToolMaster = new ConnectToolMaster(GetJson(masterJsonFileContainer, new JsonFileName("buildMenu")));
            InitializeMaster(ConnectToolMaster);

            MapObjectMaster = new MapObjectMaster(GetJson(masterJsonFileContainer, new JsonFileName("map")));
            InitializeMaster(MapObjectMaster);

            // ItemMaster, FluidMaster依存（veinParamのitemGuid/fluidGuidを検証）
            // Depends on ItemMaster, FluidMaster (validates veinParam itemGuid/fluidGuid)
            MapVeinMaster = new MapVeinMaster(GetJson(masterJsonFileContainer, new JsonFileName("map")));
            InitializeMaster(MapVeinMaster);

            // MapVeinMaster依存（VeinGuidのveinType一致を検証）。generation.jsonを持たないModは空マスタで代替する
            // Depends on MapVeinMaster (validates VeinGuid veinType match). Mods without generation.json fall back to an empty master
            GenerationMaster = TryGetJson(masterJsonFileContainer, new JsonFileName("generation"), out var generationJson)
                ? new GenerationMaster(generationJson, masterJsonFileContainer.ConfigJsons[0].ModId.AsPrimitive())
                : GenerationMaster.CreateEmpty();
            InitializeMaster(GenerationMaster);

            TrainUnitMaster = new TrainUnitMaster(GetJson(masterJsonFileContainer, new JsonFileName("train")));
            InitializeMaster(TrainUnitMaster);

            // BlockMaster, ItemMaster, FluidMaster依存
            // Depends on BlockMaster, ItemMaster, FluidMaster
            MachineRecipesMaster = new MachineRecipesMaster(GetJson(masterJsonFileContainer, new JsonFileName("machineRecipes")));
            InitializeMaster(MachineRecipesMaster);

            // cleanRoom.json を持たない Mod でも起動できるよう、欠損時は空マスタで代替する
            // Fall back to an empty master when the mod ships no cleanRoom.json, so such mods still boot
            CleanRoomMaster = TryGetJson(masterJsonFileContainer, new JsonFileName("cleanRoom"), out var cleanRoomJson)
                ? new CleanRoomMaster(cleanRoomJson)
                : CleanRoomMaster.CreateEmpty();
            InitializeMaster(CleanRoomMaster);

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

        // 任意ファイル用。存在しないjsonは呼び出し側でフォールバックする
        // For optional files; callers fall back when the json is absent
        private static bool TryGetJson(MasterJsonFileContainer masterJsonFileContainer, JsonFileName jsonFileName, out JToken jToken)
        {
            var index = 0; // TODO 現状はとりあえず一つのmodのみロードする。今後は複数のjsonファイルをロードできるようにする。
            if (!masterJsonFileContainer.ConfigJsons[index].JsonContents.TryGetValue(jsonFileName, out var jsonContent))
            {
                jToken = null;
                return false;
            }

            jToken = (JToken)JsonConvert.DeserializeObject(jsonContent);
            return true;
        }
    }
}