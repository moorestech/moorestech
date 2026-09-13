using System;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Network.API;
using Core.Update;
using Cysharp.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json.Linq;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Playtest.Progress
{
    // セッションの進行を購読で集めて追記し、終了時に1件の進行記録として書き出す（ADR 0058）
    // Collects the session's progress through subscriptions, appends it, and writes one progress record at shutdown (ADR 0058)
    public sealed class ProgressRecorder : IInitializable, IDisposable, IGameShutdownParticipant, IPlaytestProgressSink
    {
        private readonly InitialHandshakeResponse _handshake;
        private readonly UIStateControl _uiStateControl;
        private readonly IPlaytestSessionIdentity _identity;
        private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
        private readonly CompositeDisposable _eventSubscriptions = new();
        private bool _closed;

        public ProgressRecorder(InitialHandshakeResponse handshake, UIStateControl uiStateControl, IPlaytestSessionIdentity identity)
        {
            _handshake = handshake;
            _uiStateControl = uiStateControl;
            _identity = identity;
        }

        public void Initialize()
        {
            // 前回の書きかけを先に畳んでから今回を開く。current/ は常に1セッションぶんしか持たない
            // Fold the previous half-written session first; current/ never holds more than one session
            var salvage = PreviousSessionSalvage.Artifacts;
            if (salvage == null) Debug.LogWarning("前回終了の判定が退避から得られないため、残骸は正常終了として回収します");
            ProgressSessionRecovery.RecoverLeftoverSession(salvage?.PreviousExitWasClean ?? true);

            OpenSessionAsync().Forget(exception => Debug.LogError($"進行記録のヘッダを書けませんでした: {exception.GetBaseException().Message}"));

            _uiStateControl.OnStateChanged += OnUiStateChanged;
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(ResearchCompleteEventPacket.EventTag, OnResearchCompleted));
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnChallengeCompleted));
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockPlaced));

            GameShutdownEvent.RegisterParticipant(this);
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= OnUiStateChanged;
            _eventSubscriptions.Dispose();
            GameShutdownEvent.UnregisterParticipant(this);
        }

        // 終了時に record.json を書く。書き出しは同期IOなので待ちは一瞬で終わる
        // Writes record.json at shutdown; the write is synchronous IO and finishes immediately
        public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            if (_closed) return UniTask.FromResult(ShutdownFlushResult.AlreadyShutdown);
            _closed = true;

            // 閉じられないのはヘッダが無い/壊れている時だけ。記録が1件消えるので黙って終わらせない
            // Closing fails only with a missing or broken header; one lost record must never end in silence
            var bundle = ProgressRecordFiles.CloseCurrentInto(ProgressSessionRecovery.QuitEndReason, DateTime.UtcNow);
            if (bundle == null) Debug.LogWarning("今回のセッションの進行記録を書き出せませんでした（ヘッダが無いか壊れています）");
            return UniTask.FromResult(ShutdownFlushResult.Flushed);
        }

        public void RecordCraftExecuted(Guid recipeGuid)
        {
            Append(ProgressEventType.CraftExecuted, new JObject { ["recipeGuid"] = recipeGuid.ToString() });
        }

        public void RecordReportSent(string kind)
        {
            Append(ProgressEventType.ReportSent, new JObject { ["kind"] = kind });
        }

        // ヘッダはワールドのプレイ時間を待ってから1度だけ書く。先に始まったイベント追記とは順序に依存しない
        // The header is written once after the world's play time arrives; appended events do not depend on that order
        private async UniTask OpenSessionAsync()
        {
            var completedChallenges = _handshake.Challenges.SelectMany(category => category.CompletedChallenges).Select(challenge => challenge.ChallengeGuid);
            var header = new ProgressRecordHeader
            {
                SteamId = _identity.SteamId,
                BuildInfo = Application.isEditor ? null : RepositoryStateProbe.ReadBuildInfo(),
                SessionStart = _sessionStartUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                BaselineChallenges = ProgressBaseline.CompletedChallengeGuids(completedChallenges),
                BaselineResearch = ProgressBaseline.CompletedResearchGuids(_handshake.ResearchNodeStates),
            };

            var info = await ClientContext.VanillaApi.Response.GetWorldPlaySessionInfo(default);
            if (info == null) Debug.LogWarning("ワールドのプレイ時間を取得できないため worldCreatedAt と totalPlaySeconds は空で記録します");
            header.WorldCreatedAt = info?.WorldCreatedAt ?? "";
            header.TotalPlaySecondsAtStart = info?.TotalPlaySeconds ?? 0;

            ProgressRecordFiles.WriteHeader(header);
        }

        private void OnUiStateChanged(UIStateEnum state)
        {
            Append(ProgressEventType.UiStateChanged, new JObject { ["state"] = state.ToString() });
        }

        // 外部プロセスから届いたバイト列の復号は外部境界なので、壊れた1件で記録全体を止めない
        // Decoding bytes from an external process is an external boundary; one broken packet must not stop the whole record
        private void OnResearchCompleted(byte[] payload)
        {
            var message = Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload, ResearchCompleteEventPacket.EventTag);
            if (message == null) return;
            Append(ProgressEventType.ResearchCompleted, new JObject { ["researchGuid"] = message.ResearchGuidStr });
        }

        private void OnChallengeCompleted(byte[] payload)
        {
            var message = Deserialize<CompletedChallengeEventMessagePack>(payload, CompletedChallengeEventPacket.EventTag);
            if (message == null) return;
            Append(ProgressEventType.ChallengeCompleted, new JObject { ["challengeGuid"] = message.CompletedChallengeGuidStr });
        }

        // 設置数だけが集計対象。BlockId はマスタのロード順で採番される揮発値なので記録に残さない
        // Only the count is aggregated; BlockId is a volatile value renumbered per master load, so it is not recorded
        private void OnBlockPlaced(byte[] payload)
        {
            Append(ProgressEventType.BlockPlaced, new JObject());
        }

        private static T Deserialize<T>(byte[] payload, string eventTag) where T : class
        {
            // MessagePack の復号は外部プロセス由来の入力境界。ここだけ catch し、理由を残して1件を捨てる
            // MessagePack decoding is the input boundary from an external process; catch only here, log the reason and drop one packet
            try
            {
                return MessagePackSerializer.Deserialize<T>(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録に載せるイベントを復号できないため飛ばします tag:{eventTag} {exception.GetBaseException().Message}");
                return null;
            }
        }

        private void Append(string type, JObject data)
        {
            // 書き出し済みのセッションへ足すと outbox に出ない行が current/ に湧く。閉じた後は黙らず理由を残して捨てる
            // Appending to a closed session would resurrect current/ with lines no outbox holds, so it is dropped with a reason
            if (_closed)
            {
                Debug.LogWarning($"進行記録は書き出し済みのため追記しません type:{type}");
                return;
            }
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, GameUpdater.CurrentTick, type, data));
        }
    }
}
