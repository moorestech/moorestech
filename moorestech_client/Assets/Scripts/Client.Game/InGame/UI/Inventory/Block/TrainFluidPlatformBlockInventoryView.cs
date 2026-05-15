using Client.Game.InGame.Block;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // 液体貨物プラットフォーム用UI: 容量(static)を表示し、基底からトグル機能を継承
    // UI for fluid cargo platforms: shows static capacity and inherits the mode toggle
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

            capacityText.text = $"容量: {param.Capacity}";
        }
    }
}
