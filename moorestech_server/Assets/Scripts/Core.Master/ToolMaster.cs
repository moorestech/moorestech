using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.ItemsModule;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 装備ツール（斧・石器等）と装備スロット数のマスタ
    // Master for equippable tools (axe, stone tool, etc.) and the equipment slot count
    public class ToolMaster : IMasterValidator
    {
        public readonly int EquipmentSlotCount;

        // 生配列は可変なので外部へは公開せず、参照はAllのみに絞る
        // The raw array is mutable, so it stays private and All is the only exposed view
        private readonly ToolMasterElement[] _tools;

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
            // ToolMasterは追加の初期化処理がないため、空実装
            // ToolMaster has no additional initialization, so empty implementation
        }

        public IReadOnlyList<ToolMasterElement> All => _tools;
    }
}
