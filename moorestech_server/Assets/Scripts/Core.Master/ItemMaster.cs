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

        // 装着アイテムId→モジュール定義（root modules由来）
        // equipped itemId → module definition (from root modules)
        private Dictionary<ItemId, ModuleMasterElement> _moduleByItemId;

        // 基準ItemId→レベル順変種配列（先頭=基準自身）
        // baseItemId → level-ordered variants (index 0 = the base)
        private Dictionary<ItemId, ItemId[]> _levelVariantTable;

        private Dictionary<Guid, ItemStackLevelTableMasterElement> _stackLevelTableByGuid;

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
            ItemMasterUtil.Initialize(Items, out _itemElementTableById, out _itemGuidToItemId, out _moduleByItemId, out _levelVariantTable);

            // スタックレベルテーブルのGUID索引を構築
            // Build the GUID index of stack level tables
            _stackLevelTableByGuid = new Dictionary<Guid, ItemStackLevelTableMasterElement>();
            foreach (var table in Items.ItemStackLevelTables) _stackLevelTableByGuid.Add(table.TableGuid, table);
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
        
        // 揮発ItemIdをセーブ用の安定GUIDへ変換する（空アイテムはGuid.Empty）
        // Convert the volatile ItemId to a stable GUID for saving (empty item maps to Guid.Empty)
        public Guid GetItemGuid(ItemId itemId)
        {
            if (itemId == EmptyItemId)
            {
                return Guid.Empty;
            }
            return GetItemMaster(itemId).ItemGuid;
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

        // 装着アイテムに対応するモジュール定義を返す（モジュールでなければnull）
        // Return the module definition for an equipped item (null when the item is not a module)
        public ModuleMasterElement GetModuleByItemIdOrNull(ItemId itemId)
        {
            return _moduleByItemId.GetValueOrDefault(itemId);
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

        public ItemStackLevelTableMasterElement GetStackLevelTable(Guid tableGuid)
        {
            if (!_stackLevelTableByGuid.TryGetValue(tableGuid, out var table))
            {
                throw new InvalidOperationException($"ItemStackLevelTable not found. TableGuid:{tableGuid}");
            }
            return table;
        }
    }
}