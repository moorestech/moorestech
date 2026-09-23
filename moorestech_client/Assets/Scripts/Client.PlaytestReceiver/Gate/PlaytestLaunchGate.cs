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
    // 起動時照合の関所。開始経路は PlaytestTitleGates.TryPassStart 越しにここを通り、表示側は照合結果を購読する
    // The launch check's gate; start paths reach it through PlaytestTitleGates.TryPassStart, and the title view subscribes to the verdict
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
            var result = PlaytestGateDecision.Decide(true, true, authenticated, session);
            if (result.IsBlocked)
            {
                Debug.LogError($"[PlaytestReceiver] launch blocked: {result.Status} {result.Detail}");
            }

            SetCurrent(result);
        }

        // 照合の確定を購読で待つ。期限までに確定しなければ未確定の結果をそのまま返し、呼び手が期限切れとして扱う
        // Waits for the verdict to settle by subscription; past the deadline it returns the still-unsettled verdict for the caller to treat as a timeout
        public static async UniTask<PlaytestGateResult> WaitForSettledVerdictAsync(float timeoutSeconds, CancellationToken token)
        {
            using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var deadline = deadlineSource.CancelAfterSlim(TimeSpan.FromSeconds(timeoutSeconds), DelayType.Realtime);

            var (timedOut, settled) = await CurrentProperty.Where(static result => result.IsSettled).ToUniTask(true, deadlineSource.Token).SuppressCancellationThrow();
            if (!timedOut) return settled;

            // 呼び手自身の打ち切りは期限切れと混ぜず、そのまま打ち切りとして伝える
            // A cancellation by the caller is not confused with the deadline and propagates as a cancellation
            token.ThrowIfCancellationRequested();
            return CurrentProperty.Value;
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
            // 識別の設定と解除はここ1箇所。Allowed以外へ移ったら空へ戻し、前の検証済みSteamIDを次の起動・再評価へ残さない（ADR 0065）
            // Setting and clearing the identity happens only here; anything but Allowed returns it to empty so no earlier verified SteamID survives a re-evaluation (ADR 0065)
            if (result.TryGetVerifiedSteamId(out var verifiedSteamId)) PlaytestSessionIdentityProvider.SetCurrent(new ReceiverVerifiedSessionIdentity(verifiedSteamId));
            else ClearIdentity(result.Status);

            // 識別は結果を配る前に据える。購読側（タイトルのゲート・開始経路）が読む時点で検証済みSteamIDが揃っている（ADR 0065）
            // The identity is set before the verdict goes out, so subscribers (title gates, start paths) already see the verified SteamID (ADR 0065)
            CurrentProperty.Value = result;
        }

        // 消去も縮退。検証済みSteamIDが載っていた起動で消えたら、記録のsteamIdが欠ける理由を読めるよう必ず痕跡を残す（再評価中のCheckingを含む）
        // Clearing is a degradation too: when a boot that had a verified SteamID loses it, the reason the records lack a steamId is always left readable (a re-evaluation's Checking included)
        private static void ClearIdentity(PlaytestGateStatus status)
        {
            var cleared = PlaytestSessionIdentityProvider.Current.SteamId;
            if (!string.IsNullOrEmpty(cleared)) Debug.Log($"[PlaytestReceiver] 検証済みSteamIDを空へ戻します status:{status}（この間に書かれる記録・箱のsteamIdは空になります）");
            // 空になった事情は照合の結末で決まる。開発者モード以外を開発者モードと名乗らせない
            // Why it is empty follows from the verdict; anything other than developer mode never claims to be developer mode
            var absenceReason = status == PlaytestGateStatus.DeveloperMode
                ? EmptyPlaytestSessionIdentity.DeveloperModeReason
                : $"テスター識別（SteamID）が無い（起動時照合で検証済みSteamIDが得られていない status:{status}）";
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity(absenceReason));
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
