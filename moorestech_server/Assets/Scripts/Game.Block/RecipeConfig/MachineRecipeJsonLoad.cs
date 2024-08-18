using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Newtonsoft.Json;

namespace Game.Block.RecipeConfig
{
    internal class MachineRecipeJsonLoad
    {
        internal List<MachineRecipeData> LoadConfig(IBlockConfig blockConfig, IItemStackFactory itemStackFactory, List<string> configJsons)
        {
            var recipes = new List<MachineRecipeData>();
            foreach (var json in configJsons) recipes.AddRange(Load(blockConfig, itemStackFactory, json));
            
            return recipes;
        }
        
        private List<MachineRecipeData> Load(IBlockConfig blockConfig, IItemStackFactory itemStackFactory, string json)
        {
            //JSONデータの読み込み
            MachineRecipeJsonData[] data = JsonConvert.DeserializeObject<MachineRecipeJsonData[]>(json);
            
            //レシピデータを実際に使用する形式に変換
            IEnumerable<MachineRecipeData> r = data.ToList().Select((r, index) =>
            {
                var inputItem =
                    r.ItemInputs.ToList().Select(item => itemStackFactory.Create(item.ModId, item.ItemName, item.Count))
                        .ToList();
                
                
                inputItem = inputItem.OrderBy(i => i.Id).ToList();
                
                IEnumerable<ItemOutput> outputs =
                    r.ItemOutputs.Select(r =>
                        new ItemOutput(itemStackFactory.Create(r.ModId, r.ItemName, r.Count), r.Percent));
                
                var modId = r.BlockModId;
                var blockName = r.BlockName;
                
                var blockId = blockConfig.GetBlockConfig(modId, blockName).BlockId;
                
                return new MachineRecipeData(blockId, r.Time, inputItem, outputs.ToList(),
                    index);
            });
            
            return r.ToList();
        }
    }
    
    //JSONからのデータを格納するためのクラス
    [JsonObject]
    internal class MachineRecipeJsonData
    {
        [JsonProperty("blockModId")] private string _blockModId;
        [JsonProperty("blockName")] private string _blockName;
        [JsonProperty("input")] private MachineRecipeInput[] _itemInputs;
        [JsonProperty("output")] private MachineRecipeOutput[] _itemOutputs;
        [JsonProperty("time")] private float _time;
        
        public MachineRecipeOutput[] ItemOutputs => _itemOutputs;
        
        public MachineRecipeInput[] ItemInputs => _itemInputs;
        
        public float Time => _time;
        public string BlockName => _blockName;
        public string BlockModId => _blockModId;
    }
    
    [JsonObject]
    internal class MachineRecipeInput
    {
        [JsonProperty("count")] private int _count;
        [JsonProperty("itemName")] private string _itemName;
        [JsonProperty("modId")] private string _modId;
        
        public int Count => _count;
        
        public string ItemName => _itemName;
        
        public string ModId => _modId;
    }
    
    [JsonObject]
    internal class MachineRecipeOutput
    {
        [JsonProperty("count")] private int _count;
        [JsonProperty("itemName")] private string _itemName;
        [JsonProperty("modId")] private string _modId;
        [JsonProperty("percent")] private double _percent;
        
        public double Percent => _percent;
        
        public int Count => _count;
        
        public string ItemName => _itemName;
        
        public string ModId => _modId;
    }
}