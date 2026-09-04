using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Starter
{
    /// <summary>
    /// リモート接続を拒んだ理由。文言キーと、その{p0}へ供給する値を一組で運ぶ
    /// Why a remote connection was refused: the wording key together with the values its {p0} receives
    /// </summary>
    public readonly struct RemoteConnectionDenyReason
    {
        public readonly LocalizationKey Key;
        public readonly IReadOnlyList<string> TextParams;

        public RemoteConnectionDenyReason(LocalizationKey key)
        {
            Key = key;
            TextParams = Array.Empty<string>();
        }

        public RemoteConnectionDenyReason(LocalizationKey key, string textParam)
        {
            Key = key;
            TextParams = new[] { textParam };
        }
    }
}
