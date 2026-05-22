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

        // RidableType と GetSaveState() のペイロード文字列から IRidableIdentifier を復元する（セーブロード用）。
        // 未知の型はセーブの前方互換のため例外にせず null を返し、呼び出し側で読み飛ばす。
        // Restores an IRidableIdentifier from a RidableType and its GetSaveState() payload (save-load path).
        // Unknown types return null instead of throwing so the loader can skip the row.
        public static IRidableIdentifier FromSaveState(RidableType type, string saveState)
        {
            return type switch
            {
                RidableType.TrainCar => new TrainCarRidableIdentifier(long.Parse(saveState)),
                _ => null
            };
        }
    }
}
