using Game.PlayerIdentity;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // この端末の身元。セーブの players.identity と厳密一致で突き合わせるために報告へ載せる（ADR 0073）
    // This machine's identity, carried in the report so it can be matched exactly against the save's players.identity (ADR 0073)
    public static class LocalDeviceIdentity
    {
        internal const string UnavailableReason = "端末身元が無い（SystemInfo.deviceUniqueIdentifier を取得できなかった）";

        // 取れなければ空文字でなくnullを返す。空文字は「識別子が空の実端末」という実値に化ける
        // Returns null rather than an empty string when unavailable; an empty string would pose as a real machine with a blank id
        public static string Resolve()
        {
            var deviceUniqueIdentifier = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier) return null;
            return PlayerIdentityText.ForDevice(deviceUniqueIdentifier);
        }
    }
}
