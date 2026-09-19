using System;
using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UniRx;
using UnityEngine;

namespace Client.PlaytestReceiver.Gate
{
    // 起動時照合の唯一の関所。開始経路はここへ問い合わせ、表示側は照合結果を購読する
    // The single gate for the launch check; start paths ask here and the title view subscribes to the verdict
    public static class PlaytestLaunchGate
    {
        // シーン跨ぎで持ち回る必要があり、MainMenuシーンにはDIコンテナが無いのでstaticで保持する
        // The verdict must survive a scene load and the MainMenu scene has no DI container, so it is held statically
        private static readonly ReactiveProperty<PlaytestGateResult> CurrentProperty = new(PlaytestGateResult.NotEvaluated);
        public static IReadOnlyReactiveProperty<PlaytestGateResult> Current => CurrentProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            SetCurrent(PlaytestGateResult.NotEvaluated);
        }

        public static async UniTask EvaluateAsync(IPlaytestSteamTicketProvider ticketProvider, IPlaytestReceiverApi api, DateTime utcNow, CancellationToken token)
        {
            if (!RequiresCheck(ticketProvider))
            {
                SetCurrent(PlaytestGateResult.DeveloperMode);
                return;
            }

            // 判定が出るまでは開始させない。待ち文言を閉じて押すだけで照合を素通しできないようにする
            // Nothing may start before the verdict, so closing the waiting message cannot bypass the check
            SetCurrent(PlaytestGateResult.Checking);

            var session = new PlaytestSession(api, ticketProvider);
            var authenticated = await session.AuthenticateAsync(utcNow, token);
            var result = PlaytestGateDecision.Decide(true, true, authenticated.Outcome, authenticated.Detail, session);
            if (result.IsBlocked)
            {
                Debug.LogError($"[PlaytestReceiver] launch blocked: {result.Status} {result.Detail}");
            }

            // 識別は結果を配る前に据える。購読側（タイトルのゲート・開始経路）が読む時点で検証済みSteamIDが揃っている（ADR 0065）
            // The identity is set before the verdict goes out, so subscribers (title gates, start paths) already see the verified SteamID (ADR 0065)
            if (result.TryGetAllowedSession(out var allowedSession)) PlaytestSessionIdentityProvider.SetCurrent(new ReceiverVerifiedSessionIdentity(allowedSession.VerifiedSteamId));

            SetCurrent(result);
        }

        // 通れなければ理由をログへ出し、テスター向けの文言をここで解決して返す。呼び手は表示するだけ
        // A refusal is logged and its tester-facing text is resolved here; callers only display it
        public static bool TryPassStart(string callerName, out string denyReasonText)
        {
            // 未評価のまま開始が来たら遅延評価する。照合の要らない起動（Editor・自作ビルド）はここで開発者モードに確定する
            // A start before any evaluation is judged lazily; a launch that needs no check (Editor, own build) settles as developer mode here
            if (CurrentProperty.Value.Status == PlaytestGateStatus.NotEvaluated && !RequiresCheck(new PlaytestSteamTicketProvider()))
            {
                SetCurrent(PlaytestGateResult.DeveloperMode);
            }

            var current = CurrentProperty.Value;
            if (!current.IsBlocked)
            {
                denyReasonText = "";
                return true;
            }

            Debug.LogWarning($"[PlaytestReceiver] {callerName} refused: {current.Status} {current.Detail}");
            denyReasonText = Localize.Get(current.ReasonKey);
            return false;
        }

        // 照合結果を置けるのは本クラスとテストだけ。表示側・開始経路からは書き換えさせない
        // Only this class and the tests may place a verdict; views and start paths never overwrite it
        internal static void SetCurrent(PlaytestGateResult result)
        {
            CurrentProperty.Value = result;
        }

        // 配布版でSteamが動いている場合だけ照合する
        // Only a distribution build with Steam running is checked
        private static bool RequiresCheck(IPlaytestSteamTicketProvider ticketProvider)
        {
            // 素通しも縮退経路。理由を残さないと配布版で照合が効いていないことに気づけない
            // Passing through is a degraded path too; without these lines a distribution build could skip the check unnoticed
            if (!File.Exists(GameSystemPaths.BuildInfoFilePath))
            {
                Debug.Log("[PlaytestReceiver] developer mode (no build-info.json); skipping the launch check");
                return false;
            }
            if (!ticketProvider.IsSteamRunning())
            {
                Debug.Log("[PlaytestReceiver] developer mode (Steam is not running); skipping the launch check");
                return false;
            }
            return true;
        }
    }
}
