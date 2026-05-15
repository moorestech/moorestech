using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    // アイテム貨物プラットフォーム/駅用UI
    // ChestBlockInventoryViewと同じパターンでアイテムスロットを生成し、加えて基底のトグル機能を持つ
    // UI for item cargo platforms and stations. Builds item slots like ChestBlockInventoryView and inherits the mode toggle from the base
    public class TrainItemPlatformBlockInventoryView : TrainPlatformBlockInventoryViewBase
    {
        [SerializeField] private RectTransform itemSlotsParent;

        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);

            // アイテムスロット数の取得元はブロック種別ごとに異なる
            // Resolve the item slot count differently per block param type
            int slotCount = ResolveSlotCount(blockGameObject);
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
        }

        #region Internal

        private static int ResolveSlotCount(BlockGameObject blockGameObject)
        {
            // 貨物プラットフォームと駅はそれぞれ独自のBlockParamを持つが、どちらもItemSlotCountを露出している
            // Cargo platform and station have distinct BlockParam types; both expose ItemSlotCount
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
