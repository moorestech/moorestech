using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json;

namespace Game.Train.Unit.Containers
{
    // 列車車両コンテナ(アイテム/液体)の永続化用key-value表現。IDは揮発値なのでGUIDで保存する
    // Key-value representation for persisting a train car container (item/fluid); ids are volatile so GUIDs are stored
    public class TrainCarContainerSaveJsonObject
    {
        public const string ItemType = "Item";
        public const string FluidType = "Fluid";

        [JsonProperty("containerType")]
        public string ContainerType;

        [JsonProperty("items")]
        public List<ItemStackSaveJsonObject> Items;

        [JsonProperty("fluid")]
        public FluidContainerSaveJsonObject Fluid;

        public TrainCarContainerSaveJsonObject() { }

        public static TrainCarContainerSaveJsonObject FromContainer(ITrainCarContainer container)
        {
            switch (container)
            {
                case ItemTrainCarContainer itemContainer:
                    return new TrainCarContainerSaveJsonObject
                    {
                        ContainerType = ItemType,
                        Items = itemContainer.InventoryItems.Select(stack => new ItemStackSaveJsonObject(stack)).ToList(),
                    };
                case FluidTrainCarContainer fluidContainer:
                    return new TrainCarContainerSaveJsonObject
                    {
                        ContainerType = FluidType,
                        Fluid = new FluidContainerSaveJsonObject(fluidContainer.Container),
                    };
                default:
                    return null;
            }
        }

        public ITrainCarContainer ToContainer(TrainCarMasterElement master)
        {
            switch (ContainerType)
            {
                case ItemType:
                    // マスタ定義に合わせて配列を切り詰め、拡張する
                    // Trims or extends the array to match the master definition.
                    return ItemTrainCarContainer.CreateWithInventoryItems(BuildItemStacks(master.InventorySlots));
                case FluidType:
                    return new FluidTrainCarContainer(Fluid.ToFluidContainer(master.FluidCapacity));
                default:
                    return null;
            }

            #region Internal

            IItemStack[] BuildItemStacks(int slotCount)
            {
                var stacks = new IItemStack[slotCount];
                for (var i = 0; i < slotCount; i++)
                {
                    stacks[i] = i < Items.Count ? Items[i].ToItemStack() : ServerContext.ItemStackFactory.CreatEmpty();
                }
                return stacks;
            }

            #endregion
        }
    }
}
