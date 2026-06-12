using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct ItemId { }
    
    public class ItemMaster : IMasterValidator
    {
        public static readonly ItemId EmptyItemId = new(0);

        public readonly Items Items;

        private Dictionary<ItemId, ItemMasterElement> _itemElementTableById;
        private Dictionary<Guid, ItemId> _itemGuidToItemId;

        // 基準ItemId → レベル順の変種ItemId配列（index 0 = レベル1の基準アイテム自身）
        // baseItemId → level-ordered variant ItemId array (index 0 = level 1, the base item itself)
        private Dictionary<ItemId, ItemId[]> _levelVariantTable;

        public ItemMaster(JToken itemJToken)
        {
            Items = ItemsLoader.Load(itemJToken);
        }

        public bool Validate(out string errorLogs)
        {
            return ItemMasterUtil.Validate(Items, out errorLogs);
        }

        public void Initialize()
        {
            ItemMasterUtil.Initialize(Items, out _itemElementTableById, out _itemGuidToItemId, out _levelVariantTable);
        }
        
        public ItemMasterElement GetItemMaster(ItemId itemId)
        {
            if (!_itemElementTableById.TryGetValue(itemId, out var element))
            {
                throw new InvalidOperationException($"ItemElement not found. ItemId:{itemId}");
            }
            return element;
        }
        
        public ItemMasterElement GetItemMaster(Guid itemGuid)
        {
            var itemId = GetItemId(itemGuid);
            return GetItemMaster(itemId);
        }
        
        public ItemId GetItemId(Guid itemGuid)
        {
            if (itemGuid == Guid.Empty)
            {
                return EmptyItemId;
            }
            
            var result = GetItemIdOrNull(itemGuid);
            if (result == null)
            {
                throw new InvalidOperationException($"ItemElement not found. ItemGuid:{itemGuid}");
            }
            return result.Value;
        }
        
        
        public ItemId? GetItemIdOrNull(Guid itemGuid)
        {
            if (!_itemGuidToItemId.TryGetValue(itemGuid, out var itemId))
            {
                return null;
            }
            return itemId;
        }
        
        public bool ExistItemId(Guid guid)
        {
            return _itemGuidToItemId.ContainsKey(guid);
        }

        public bool ExistItemId(ItemId itemId)
        {
            return _itemElementTableById.ContainsKey(itemId);
        }
        
        public IEnumerable<ItemId> GetItemAllIds()
        {
            return _itemElementTableById.Keys;
        }

        public bool HasLevelFamily(ItemId baseItemId)
        {
            return _levelVariantTable.ContainsKey(baseItemId);
        }

        public ItemId GetLevelVariantItemId(ItemId baseItemId, int level)
        {
            // レベルは1始まり。範囲外は[1, 最大レベル]へクランプする
            // Levels are 1-based; out-of-range values are clamped into [1, max level]
            var variants = _levelVariantTable[baseItemId];
            var index = Math.Clamp(level - 1, 0, variants.Length - 1);
            return variants[index];
        }
    }
}