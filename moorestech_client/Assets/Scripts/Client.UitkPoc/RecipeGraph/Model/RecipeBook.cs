using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Client.UitkPoc.RecipeGraph.Model
{
    // masterのJSONから「何を何で作るか」だけを読んだ索引。PoCなのでMasterHolderを経由せず直接読む
    // Index of "what is made by what" read straight from master JSON, bypassing MasterHolder for the PoC
    public class RecipeBook
    {
        public readonly Dictionary<string, string> ResourceNames = new();
        public readonly HashSet<string> FluidGuids = new();
        public readonly Dictionary<string, List<RecipeEntry>> RecipesByOutput = new();
        public readonly Dictionary<string, MinerEntry> MinersByResource = new();

        public RecipeBook(string modDirectory)
        {
            var masterDirectory = Path.Combine(modDirectory, "master");
            foreach (var item in Read("items.json")) ResourceNames[(string)item["itemGuid"]] = (string)item["name"];
            foreach (var fluid in Read("fluids.json"))
            {
                ResourceNames[(string)fluid["fluidGuid"]] = (string)fluid["name"];
                FluidGuids.Add((string)fluid["fluidGuid"]);
            }

            // ブロック名を引けるようにしてからレシピと採掘設定を読む
            // Resolve block names first, then read recipes and mining settings
            var blocks = Read("blocks.json").ToDictionary(b => (string)b["blockGuid"]);
            foreach (var recipe in Read("machineRecipes.json")) AddRecipe(recipe, (string)blocks[(string)recipe["blockGuid"]]["name"]);
            foreach (var block in blocks.Values) AddMiner(block);

            #region Internal

            IEnumerable<JToken> Read(string fileName)
            {
                return JObject.Parse(File.ReadAllText(Path.Combine(masterDirectory, fileName)))["data"];
            }

            void AddRecipe(JToken recipe, string machineName)
            {
                var inputs = recipe["inputItems"].Where(i => (bool?)i["isRemain"] != true).Select(i => new ResourceAmount((string)i["itemGuid"], (float)i["count"]))
                    .Concat(recipe["inputFluids"].Select(f => new ResourceAmount((string)f["fluidGuid"], (float)f["amount"]))).ToList();
                var outputs = recipe["outputItems"].Select(o => new ResourceAmount((string)o["itemGuid"], (float)o["count"]))
                    .Concat(recipe["outputFluids"].Select(f => new ResourceAmount((string)f["fluidGuid"], (float)f["amount"]))).ToList();
                var entry = new RecipeEntry(machineName, (float)recipe["time"], inputs, outputs);
                foreach (var output in outputs)
                {
                    if (!RecipesByOutput.TryGetValue(output.ResourceGuid, out var list)) RecipesByOutput[output.ResourceGuid] = list = new List<RecipeEntry>();
                    list.Add(entry);
                }
            }

            void AddMiner(JToken block)
            {
                var mineSettings = block["blockParam"]?["mineSettings"];
                if (mineSettings == null) return;

                // 同じ資源を掘れる採掘機が複数ある場合は「原始的」を優先する
                // Prefer the primitive miner when several miners dig the same resource
                var name = (string)block["name"];
                foreach (var setting in mineSettings)
                {
                    var guid = (string)setting["itemGuid"];
                    var isPrimitive = name.Contains("原始的");
                    if (MinersByResource.TryGetValue(guid, out var existing) && (!isPrimitive || existing.MachineName.Contains("原始的"))) continue;
                    MinersByResource[guid] = new MinerEntry(name, (float)setting["time"]);
                }
            }

            #endregion
        }

        // 既定レシピは「原始的」を含む機械を優先し、無ければ先頭
        // Default recipe prefers a primitive machine, otherwise the first one
        public int DefaultRecipeIndex(string resourceGuid)
        {
            var recipes = RecipesByOutput[resourceGuid];
            var primitive = recipes.FindIndex(r => r.MachineName.Contains("原始的"));
            return primitive >= 0 ? primitive : 0;
        }
    }

    public readonly struct ResourceAmount
    {
        public readonly string ResourceGuid;
        public readonly float Amount;

        public ResourceAmount(string resourceGuid, float amount)
        {
            ResourceGuid = resourceGuid;
            Amount = amount;
        }
    }

    public class RecipeEntry
    {
        public readonly string MachineName;
        public readonly float TimeSeconds;
        public readonly List<ResourceAmount> Inputs;
        public readonly List<ResourceAmount> Outputs;

        public RecipeEntry(string machineName, float timeSeconds, List<ResourceAmount> inputs, List<ResourceAmount> outputs)
        {
            MachineName = machineName;
            TimeSeconds = timeSeconds;
            Inputs = inputs;
            Outputs = outputs;
        }
    }

    public class MinerEntry
    {
        public readonly string MachineName;
        public readonly float TimeSeconds;

        public MinerEntry(string machineName, float timeSeconds)
        {
            MachineName = machineName;
            TimeSeconds = timeSeconds;
        }
    }
}
