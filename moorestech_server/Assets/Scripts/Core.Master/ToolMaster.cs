using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 装備ツール（斧・石器等）のマスタ。生ロードとItemId索引のみを保持する
    // Master for equippable tools (axe, stone tool, etc.); holds only raw load and an ItemId index
    public class ToolMaster : IMasterValidator
    {
        public readonly ToolMasterElement[] Tools;
        public readonly int EquipmentSlotCount;

        // 装備可能アイテムIdの集合（IsTool判定用）
        // Set of equippable itemIds (for IsTool lookup)
        private HashSet<ItemId> _toolItemIds;

        public ToolMaster(JToken itemJToken)
        {
            var items = ItemsLoader.Load(itemJToken);
            Tools = items.Tools;
            EquipmentSlotCount = items.EquipmentSlotCount;
        }

        public bool Validate(out string errorLogs)
        {
            return ToolMasterUtil.Validate(Tools, out errorLogs);
        }

        public void Initialize()
        {
            _toolItemIds = new HashSet<ItemId>();
            foreach (var element in Tools)
            {
                _toolItemIds.Add(MasterHolder.ItemMaster.GetItemId(element.ToolItemGuid));
            }
        }

        public IReadOnlyList<ToolMasterElement> All => Tools;

        public bool IsTool(ItemId itemId)
        {
            return _toolItemIds.Contains(itemId);
        }
    }
}
