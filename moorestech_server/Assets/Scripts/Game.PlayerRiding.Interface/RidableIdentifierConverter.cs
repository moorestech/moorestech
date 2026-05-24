using System;

namespace Game.PlayerRiding.Interface
{
    // IRidableIdentifier と各種シリアライズ表現を相互変換する
    // Converts between IRidableIdentifier and various serialized representations.
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
            if (messagePack.RidableType == RidableType.TrainCar)
            {
                return new TrainCarRidableIdentifier(messagePack.TrainCarInstanceId);
            }
            
            throw new ArgumentException($"Unknown RidableType: {messagePack.RidableType}");
        }

        // ロード用の作成メソッド
        // Factory method for loading from a save state string.
        public static IRidableIdentifier FromSaveState(RidableType type, string saveState)
        {
            if (type == RidableType.TrainCar)
            {
                // 不正だったらそのままスルー
                // If invalid, just return null.
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
