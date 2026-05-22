using System;

namespace Game.PlayerRiding.Interface
{
    // IRidableIdentifier と各種シリアライズ表現を相互変換する（ISubInventoryIdentifierExtension に倣う）。
    // Converts between IRidableIdentifier and its serialized representations, mirroring ISubInventoryIdentifierExtension.
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
            var type = new RidableType(messagePack.RidableType);
            if (type == RidableType.TrainCar)
            {
                return new TrainCarRidableIdentifier(long.Parse(messagePack.TrainCarInstanceId));
            }
            throw new ArgumentException($"Unknown RidableType: {messagePack.RidableType}");
        }

        // RidableType と GetSaveState() のペイロード文字列から IRidableIdentifier を復元する（セーブロード用）。
        // 未知の型はセーブの前方互換のため例外にせず null を返し、呼び出し側で読み飛ばす。
        // Restores an IRidableIdentifier from a RidableType and its GetSaveState() payload (save-load path).
        // Unknown types return null instead of throwing so the loader can skip the row.
        public static IRidableIdentifier FromSaveState(RidableType type, string saveState)
        {
            if (type == RidableType.TrainCar)
            {
                return new TrainCarRidableIdentifier(long.Parse(saveState));
            }
            return null;
        }
    }
}
