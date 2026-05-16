using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // アイテム貨物プラットフォーム/駅用UI: ChestUIと同パターンでスロット生成し、基底からトグル機能を継承
    // UI for item cargo platforms and stations: builds slots like ChestUI and inherits the mode toggle
    public class TrainItemPlatformBlockInventoryView : TrainPlatformBlockInventoryViewBase
    {
        [SerializeField] private RectTransform itemSlotsParent;

        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);

            // アイテムスロット数の取得元はブロック種別ごとに異なる
            // Resolve the item slot count differently per block param type
            int slotCount = ResolveSlotCount();
            if (slotCount <= 0)
            {
                var blockName = blockGameObject.BlockMasterElement.Name;
                var guid = blockGameObject.BlockMasterElement.BlockGuid;
                Debug.LogError($"ブロック名:{blockName} guid:{guid} はTrainItem/Station系のBlockParamを持っていません。指定しているUIまたはスキーマを見直してください。");
                return;
            }

            var itemList = new List<IItemStack>();
            for (var i = 0; i < slotCount; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, itemSlotsParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }

            UpdateItemList(itemList);

            #region Internal

            int ResolveSlotCount()
            {
                // 貨物PFと駅はそれぞれ独自BlockParamを持つが、どちらもItemSlotCountを露出する
                // Cargo platform and station expose ItemSlotCount via distinct BlockParam types
                switch (blockGameObject.BlockMasterElement.BlockParam)
                {
                    case TrainItemPlatformBlockParam item: return item.ItemSlotCount;
                    case TrainStationBlockParam station: return station.ItemSlotCount;
                    default: return 0;
                }
            }

            #endregion
        }
    }
}
