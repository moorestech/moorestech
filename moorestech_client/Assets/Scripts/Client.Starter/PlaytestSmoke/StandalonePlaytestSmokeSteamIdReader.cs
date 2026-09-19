using System;
using Steamworks;
using UnityEngine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 報告を送ったSteamアカウントのSteamIDを読む。受け口は認証チケットの持ち主のSteamIDで報告を置くので、同じ値になる
    /// Reads the SteamID of the account that sent the report; the receiver stores reports under the ticket owner's SteamID, so the values match
    /// </summary>
    internal static class StandalonePlaytestSmokeSteamIdReader
    {
        // 読めたら true とSteamIDを返す。読めなければ false と理由を返す
        // Returns true with the SteamID when readable, or false with the reason
        public static bool TryRead(out string steamId, out string failureReason)
        {
            // ネイティブ呼び出しはSteam未初期化・dll不在で例外になる外部境界。畳んで理由付きの失敗にする
            // The native call throws when Steam is uninitialized or the dll is absent; this external boundary folds it into a reasoned failure
            ulong rawSteamId;
            try
            {
                rawSteamId = SteamUser.GetSteamID().m_SteamID;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlaytestSmoke] SteamUser.GetSteamID failed: {exception.GetBaseException().Message}");
                steamId = "";
                failureReason = $"SteamUser.GetSteamID failed: {exception.GetBaseException().Message}";
                return false;
            }

            if (rawSteamId == 0)
            {
                steamId = "";
                failureReason = "SteamUser.GetSteamID returned an invalid (zero) id";
                return false;
            }

            steamId = rawSteamId.ToString();
            failureReason = "";
            return true;
        }
    }
}
