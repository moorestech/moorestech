using Client.Game.InGame.Block;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // 液体貨物プラットフォーム用UI
    // モード切替に加え、容量を表示する。現在液体量のライブ表示はFluidContainerStateDetail未整備のため省略
    // UI for fluid cargo platforms. Provides mode toggle + capacity label. Live fluid amount is omitted until a dedicated state detail exists
    public class TrainFluidPlatformBlockInventoryView : TrainPlatformBlockInventoryViewBase
    {
        [SerializeField] private TMP_Text capacityText;

        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);

            if (blockGameObject.BlockMasterElement.BlockParam is not TrainFluidPlatformBlockParam param)
            {
                var blockName = blockGameObject.BlockMasterElement.Name;
                var guid = blockGameObject.BlockMasterElement.BlockGuid;
                Debug.LogError($"ブロック名:{blockName} guid:{guid} はTrainFluidPlatformBlockParamを持っていません。指定しているUIまたはスキーマを見直してください。");
                return;
            }

            if (capacityText != null)
            {
                capacityText.text = $"容量: {param.Capacity}";
            }
        }
    }
}
