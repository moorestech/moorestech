using Game.PlayerIdentity;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // この端末の身元。どの生値を身元として認めるかの唯一のガードで、セーブの players.identity と同じ文字列を作る（ADR 0073）
    // This machine's identity; the single guard over which raw value counts, producing the same string as the save's players.identity (ADR 0073)
    public static class LocalDeviceIdentity
    {
        internal const string UnavailableReason = "端末身元が無い（SystemInfo.deviceUniqueIdentifier を取得できなかった）";

        // 取れなければ空文字でなくnullを返す。空文字は「識別子が空の実端末」という実値に化ける
        // Returns null rather than an empty string when unavailable; an empty string would pose as a real machine with a blank id
        public static string Resolve()
        {
            return TryResolve(SystemInfo.deviceUniqueIdentifier, out var identity) ? identity : null;
        }

        public static bool TryResolve(string deviceUniqueIdentifier, out string identity)
        {
            identity = null;
            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier) return false;
            identity = PlayerIdentityText.ForDevice(deviceUniqueIdentifier);
            return true;
        }
    }
}
