using System;

namespace Client.Starter.Identity
{
    // 開始前にプレイヤー身元を決められなかった。表示はローカライズキーで行う
    // The player identity could not be settled before start; display goes through the localization key
    public class PlayerStartRefusedException : Exception
    {
        public readonly string LocalizationKey;

        public PlayerStartRefusedException(string localizationKey, string logReason) : base(logReason)
        {
            LocalizationKey = localizationKey;
        }
    }
}
