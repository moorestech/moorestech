using System;
using Server.Util.MessagePack;

namespace Game.PlayerRiding.Interface
{
    // IRidableIdentifier と RidableIdentifierMessagePack の相互変換。
    // ISubInventoryIdentifierExtension と SubscribeInventoryProtocol.ConvertIdentifier に倣う。
    // Two-way conversion between IRidableIdentifier and RidableIdentifierMessagePack.
    public static class RidableIdentifierConverter
    {
        public static RidableIdentifierMessagePack ToMessagePack(this IRidableIdentifier identifier)
        {
            return identifier switch
            {
                TrainCarRidableIdentifier trainCar => RidableIdentifierMessagePack.CreateTrainCarMessage(trainCar.TrainCarInstanceId),
                _ => throw new ArgumentException($"Unknown IRidableIdentifier type: {identifier.GetType()}")
            };
        }

        public static IRidableIdentifier FromMessagePack(RidableIdentifierMessagePack messagePack)
        {
            return messagePack.RidableType switch
            {
                RidableType.TrainCar => new TrainCarRidableIdentifier(long.Parse(messagePack.TrainCarInstanceId)),
                _ => throw new ArgumentException($"Unknown RidableType: {messagePack.RidableType}")
            };
        }
    }
}
