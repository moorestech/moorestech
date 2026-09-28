using System;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Identity
{
    // 開始前にプレイヤー身元を決められなかった。表示はローカライズキーで行う
    // The player identity could not be settled before start; display goes through the localization key
    public class PlayerStartRefusedException : Exception
    {
        public readonly LocalizationKey LocalizationKey;

        internal PlayerStartRefusedException(LocalizationKey localizationKey, string logReason) : base(logReason)
        {
            LocalizationKey = localizationKey;
        }
    }
}
