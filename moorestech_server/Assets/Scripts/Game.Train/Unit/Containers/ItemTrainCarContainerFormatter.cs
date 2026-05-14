using Core.Item.Interface;
using Core.Master;
using Game.Context;
using MessagePack;
using MessagePack.Formatters;

namespace Game.Train.Unit.Containers
{
    // TODO: IItemStack自体をMessagePackでシリアライズできるようにしてこのformatterは消す
    // TODO: Drop this formatter once IItemStack itself becomes MessagePack-serializable.
    public class ItemTrainCarContainerFormatter : IMessagePackFormatter<ItemTrainCarContainer>
    {
        public void Serialize(ref MessagePackWriter writer, ItemTrainCarContainer value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            var items = value.InventoryItems;
            writer.WriteArrayHeader(items.Count);
            foreach (var stack in items)
            {
                writer.WriteArrayHeader(2);
                MessagePackSerializer.Serialize(ref writer, stack.Id, options);
                writer.WriteInt32(stack.Count);
            }
        }

        public ItemTrainCarContainer Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            var length = reader.ReadArrayHeader();
            var stacks = new IItemStack[length];
            for (var i = 0; i < length; i++)
            {
                // 各スロットエントリは[itemId, count]の2要素配列。違う長さは破損データとして拒否する
                // Each slot entry is a 2-element [itemId, count] array; reject anything else as corrupt data.
                var entryLength = reader.ReadArrayHeader();
                if (entryLength != 2)
                {
                    throw new MessagePackSerializationException(
                        $"ItemTrainCarContainer slot entry must be a 2-element array, got {entryLength} at slot {i}.");
                }

                var id = MessagePackSerializer.Deserialize<ItemId>(ref reader, options);
                var count = reader.ReadInt32();
                stacks[i] = ServerContext.ItemStackFactory.Create(id, count);
            }

            return ItemTrainCarContainer.CreateWithInventoryItems(stacks);
        }
    }
}
