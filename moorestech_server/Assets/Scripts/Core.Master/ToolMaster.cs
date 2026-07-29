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
        public readonly int EquipmentSlotCount;

        // 生配列は可変なので外部へは公開せず、参照はAllのみに絞る
        // The raw array is mutable, so it stays private and All is the only exposed view
        private readonly ToolMasterElement[] _tools;

        // 装備可能アイテムIdの集合（IsTool判定用）
        // Set of equippable itemIds (for IsTool lookup)
        private HashSet<ItemId> _toolItemIds;

        public ToolMaster(JToken itemJToken)
        {
            var items = ItemsLoader.Load(itemJToken);
            _tools = items.Tools;
            EquipmentSlotCount = items.EquipmentSlotCount;
        }

        public bool Validate(out string errorLogs)
        {
            return ToolMasterUtil.Validate(_tools, out errorLogs);
        }

        public void Initialize()
        {
            _toolItemIds = new HashSet<ItemId>();
            foreach (var element in _tools)
            {
                _toolItemIds.Add(MasterHolder.ItemMaster.GetItemId(element.ToolItemGuid));
            }
        }

        public IReadOnlyList<ToolMasterElement> All => _tools;

        public bool IsTool(ItemId itemId)
        {
            return _toolItemIds.Contains(itemId);
        }
    }
}
