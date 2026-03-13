using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using MessagePack.Resolvers;

namespace Server.Util.MessagePack
{
    public static class MessagePackInitializer
    {
        public static void Initialize()
        {        
            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new []
                {
                    new ItemTrainCarContainerSlotFormatter()
                },
                new []
                {
                    new TrainCarContainerResolver()
                }
            ));
        }
    }
}