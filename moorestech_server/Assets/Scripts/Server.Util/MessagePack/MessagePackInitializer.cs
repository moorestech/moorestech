using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Server.Util.MessagePack
{
    public static class MessagePackInitializer
    {
        public static void Initialize()
        {
            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    new ItemTrainCarContainerSlotFormatter()
                },
                new IFormatterResolver[]
                {
                    new TrainCarContainerResolver(),
                    StandardResolver.Instance
                }
            ));
        }
    }
}