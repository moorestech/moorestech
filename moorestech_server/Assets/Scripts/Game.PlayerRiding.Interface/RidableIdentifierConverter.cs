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
                // パケットは外部データ。不正なペイロードは ArgumentException で決定的に失敗させる。
                // Packets are external data; fail deterministically with ArgumentException on a malformed payload.
                if (!long.TryParse(messagePack.TrainCarInstanceId, out var instanceId))
                {
                    throw new ArgumentException($"Invalid TrainCarInstanceId: {messagePack.TrainCarInstanceId}");
                }
                return new TrainCarRidableIdentifier(instanceId);
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
                // 不正なペイロードは前方互換のため例外にせず null を返し、呼び出し側で読み飛ばす。
                // A malformed payload returns null instead of throwing so the loader can skip the row.
                if (!long.TryParse(saveState, out var instanceId))
                {
                    return null;
                }
                return new TrainCarRidableIdentifier(instanceId);
            }
            return null;
        }
    }
}
