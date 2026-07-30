using Mooresmaster.Loader.ItemsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 装備スロット数のマスタ
    // Master for the equipment slot count
    public class ToolMaster : IMasterValidator
    {
        public readonly int EquipmentSlotCount;

        public ToolMaster(JToken itemJToken)
        {
            var items = ItemsLoader.Load(itemJToken);
            EquipmentSlotCount = items.EquipmentSlotCount;
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            return true;
        }

        public void Initialize()
        {
            // ToolMasterは追加の初期化処理がないため、空実装
            // ToolMaster has no additional initialization, so empty implementation
        }
    }
}
