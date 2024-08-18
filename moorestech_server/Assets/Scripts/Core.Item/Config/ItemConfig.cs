using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface.Config;
using Core.Master;
using UnityEngine;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private const int DefaultItemMaxCount = int.MaxValue;
        private readonly Dictionary<long, int> _bockHashToId = new();
        private readonly List<ItemConfigData> _itemConfigList;
        private readonly Dictionary<string, List<int>> _modIdToItemIds = new();
        
        public ItemConfig(ConfigJsonFileContainer configPath)
        {
            _itemConfigList = ItemConfigLoad.LoadFromJsons(configPath.ItemConfigs, configPath.SortedModIds);
            
            //実際のIDは1から（空IDの次の値）始まる
            for (var itemId = ItemConst.EmptyItemId + 1; itemId <= _itemConfigList.Count; itemId++)
            {
                var arrayIndex = itemId - 1;
                if (_bockHashToId.ContainsKey(_itemConfigList[arrayIndex].ItemHash))
                    //TODO ログ基盤に入れる
                    throw new Exception("アイテム名 " + _itemConfigList[arrayIndex].Name + " は重複しています。");
                _bockHashToId.Add(_itemConfigList[arrayIndex].ItemHash, itemId);
                
                if (_modIdToItemIds.TryGetValue(_itemConfigList[arrayIndex].ModId, out var itemIds))
                    itemIds.Add(itemId);
                else
                    _modIdToItemIds.Add(_itemConfigList[arrayIndex].ModId, new List<int> { itemId });
            }
        }
        
        public IReadOnlyList<IItemConfigData> ItemConfigDataList => _itemConfigList;
        
        public IItemConfigData GetItemConfig(int id)
        {
            //0は何も持っていないことを表すので-1してListのindexにする
            id -= 1;
            if (id < 0)
                //TODO ログ基盤に入れる
                throw new ArgumentException("id must be greater than 0 ID:" + id);
            if (id < _itemConfigList.Count) return _itemConfigList[id];
            
            //TODO ログ基盤に入れる
            return new ItemConfigData("undefined id " + id, DefaultItemMaxCount, "mod is not found", id);
        }
        
        public IItemConfigData GetItemConfig(long itemHash)
        {
            return GetItemConfig(GetItemId(itemHash));
        }
        
        public int GetItemId(long itemHash)
        {
            if (_bockHashToId.TryGetValue(itemHash, out var id)) return id;
            //TODO ログ基盤に入れる
            Debug.Log("itemHash:" + itemHash + " is not found");
            return ItemConst.EmptyItemId;
        }
        
        public List<int> GetItemIds(string modId)
        {
            if (modId == null) throw new ArgumentException("Mod id is null");
            return _modIdToItemIds.TryGetValue(modId, out var itemIds) ? itemIds : new List<int>();
        }
        
        
        public int GetItemId(string modId, string itemName, string callerMethodName = "")
        {
            foreach (var itemId in GetItemIds(modId).Where(i => GetItemConfig(i).Name == itemName)) return itemId;
            //TODO ログ基盤に入れる
            Debug.Log($"itemName:{itemName} itemModId:{modId} is not found callerMethodName:{callerMethodName}");
            return ItemConst.EmptyItemId;
        }
    }
}