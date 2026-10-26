using Client.Localization;
using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Starter
{
    /// <summary>
    /// リモート接続を拒んだ理由。文言キーと、その名前付き枠へ供給する値を一組で運ぶ
    /// Why a remote connection was refused: the wording key together with the values its named placeholders receive
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
