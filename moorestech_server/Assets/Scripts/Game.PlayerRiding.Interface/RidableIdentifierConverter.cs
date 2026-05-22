using System;

namespace Game.PlayerRiding.Interface
{
    // IRidableIdentifier と RidableIdentifierMessagePack を相互変換する（ISubInventoryIdentifierExtension に倣う）。
    // Two-way conversion between IRidableIdentifier and RidableIdentifierMessagePack, mirroring ISubInventoryIdentifierExtension.
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
