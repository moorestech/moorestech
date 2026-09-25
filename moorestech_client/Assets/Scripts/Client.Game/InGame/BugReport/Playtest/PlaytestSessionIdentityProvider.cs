using UnityEngine;

namespace Client.Game.InGame.BugReport.Playtest
{
    // テスター識別の唯一の差し替え点。DI登録も開始ゲートもここを読むので、解決先が2箇所に割れない（ADR 0060 裁定4）
    // The sole seam for the tester identity; both the DI registration and the start gates read it, so the resolution never splits in two (ADR 0060 adjudication 4)
    // 開始ゲートは DI 確立前に走るためコンテナからは解決できない。起動時のローカルSteamIDは SetCurrent で差し込む
    // The start gates run before the container exists and cannot resolve from it; the local SteamID at startup pushes its implementation in via SetCurrent
    public static class PlaytestSessionIdentityProvider
    {
        private static IPlaytestSessionIdentity _current = new EmptyPlaytestSessionIdentity("テスター識別（SteamID）が無い（起動時のローカルSteamIDがまだ差し込まれていない）");

        public static IPlaytestSessionIdentity Current => _current;

        public static void SetCurrent(IPlaytestSessionIdentity identity)
        {
            if (identity == null)
            {
                Debug.LogError("PlaytestSessionIdentityProvider: nullの識別は受け付けません（既定の空SteamIDのまま続行します）");
                return;
            }
            _current = identity;
        }
    }
}
