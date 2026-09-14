using System;
using System.Threading;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Gate
{
    // 起動時照合の唯一の関所。開始経路（ローカル開始・サーバー接続）はここへ問い合わせてから進む
    // The single gate for the launch check; every start path asks here before proceeding
    public static class PlaytestLaunchGate
    {
        // シーン跨ぎで持ち回る必要があり、MainMenuシーンにはDIコンテナが無いのでstaticで保持する
        // The verdict must survive a scene load and the MainMenu scene has no DI container, so it is held statically
        public static PlaytestGateResult Current { get; private set; } = PlaytestGateDecision.DeveloperMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            Current = PlaytestGateDecision.DeveloperMode;
        }

        public static void SetCurrent(PlaytestGateResult result)
        {
            Current = result;
        }

        // 配布版でSteamが動いている場合だけ照合する。表示側も開始側もこの1箇所へ聞く
        // Only a distribution build with Steam running is checked; both the view and the start paths ask here
        public static bool RequiresCheck(IPlaytestSteamTicketProvider ticketProvider)
        {
            var hasBuildInfo = PlaytestBuildInfoFile.Exists();
            var isSteamRunning = ticketProvider.IsSteamRunning();
            if (hasBuildInfo && isSteamRunning) return true;

            // 素通しも縮退経路。理由を残さないと配布版で照合が効いていないことに気づけない
            // Passing through is a degraded path too; without this line a distribution build could skip the check unnoticed
            Debug.Log($"[PlaytestReceiver] developer mode (buildInfo={hasBuildInfo}, steam={isSteamRunning}); skipping the launch check");
            return false;
        }

        public static async UniTask<PlaytestGateResult> EvaluateAsync(PlaytestSession session, IPlaytestSteamTicketProvider ticketProvider, DateTime utcNow, CancellationToken token)
        {
            if (!RequiresCheck(ticketProvider))
            {
                SetCurrent(PlaytestGateDecision.DeveloperMode);
                return Current;
            }

            var authenticated = await session.AuthenticateAsync(utcNow, token);
            var result = PlaytestGateDecision.Decide(true, true, authenticated.Outcome, authenticated.Detail);
            if (result.IsBlocked)
            {
                Debug.LogError($"[PlaytestReceiver] launch blocked: {result.Status} {result.Detail}");
            }

            SetCurrent(result);
            return result;
        }

        // 開始を拒否したら必ず理由をログへ出す。無音で押せないボタンにしない
        // Every refusal logs its reason; a silently dead button is forbidden
        public static bool RejectStart(string callerName)
        {
            if (!Current.IsBlocked) return false;
            Debug.LogWarning($"[PlaytestReceiver] {callerName} refused: {Current.Status} {Current.Detail}");
            return true;
        }
    }
}
