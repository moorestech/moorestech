using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Crafting.Interface;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Crafting.Config
{
    public class CraftConfigJsonLoad
    {
        private readonly IItemStackFactory _itemStackFactory;
        
        public CraftConfigJsonLoad(IItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }
        
        public List<CraftingConfigInfo> Load(List<string> jsons)
        {
            var loadedData = jsons.SelectMany(JsonConvert.DeserializeObject<CraftConfigDataElement[]>).ToList();
            
            var result = new List<CraftingConfigInfo>();
            
            for (var i = 0; i < loadedData.Count; i++)
            {
                var config = loadedData[i];
                var items = new List<CraftRequiredItemInfo>();
                foreach (var craftItem in config.RequiredItems)
                {
                    if (string.IsNullOrEmpty(craftItem.ItemName) || string.IsNullOrEmpty(craftItem.ModId))
                    {
                        items.Add(new CraftRequiredItemInfo(_itemStackFactory.CreatEmpty(), false));
                        continue;
                    }
                    
                    items.Add(new CraftRequiredItemInfo(
                        _itemStackFactory.Create(craftItem.ModId, craftItem.ItemName, craftItem.Count),
                        craftItem.IsRemain));
                }
                
                //TODO ロードした時にあるべきものがなくnullだったらエラーを出す
                if (config.ResultItem.ModId == null) Debug.Log(i + " : Result item is null");
                
                var resultItem =
                    _itemStackFactory.Create(config.ResultItem.ModId, config.ResultItem.ItemName, config.ResultItem.Count);
                
                result.Add(new CraftingConfigInfo(items, resultItem, i));
            }
            
            return result;
        }
    }
}