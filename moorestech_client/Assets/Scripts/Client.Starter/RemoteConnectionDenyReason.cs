using Client.Localization;
using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Starter
{
    /// <summary>
    /// ・文言キーと名前付き値の組
    /// Pairs a wording key with named values
    /// </summary>
    public readonly struct RemoteConnectionDenyReason
    {
        public readonly LocalizationKey Key;
        public readonly IReadOnlyList<LocalizationTextParam> TextParams;

        public RemoteConnectionDenyReason(LocalizationKey key)
        {
            Key = key;
            TextParams = Array.Empty<LocalizationTextParam>();
        }

        public RemoteConnectionDenyReason(LocalizationKey key, LocalizationTextParam textParam)
        {
            Key = key;
            TextParams = new[] { textParam };
        }
    }
}
