using System;
using System.Linq;
using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Network.API;
using Core.Update;
using Cysharp.Threading.Tasks;
using Server.Boot;
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
        private readonly CancellationTokenSource _sessionCancellation = new();
        private readonly ProgressSessionWriter _writer = new();

        private GameShutdownReason _shutdownReason = GameShutdownReason.IntentionalExit;
        private int _placedBlockCount;
        private bool _started;
        private bool _pushWithoutSessionLogged;

        public ProgressRecorder(InitialHandshakeResponse handshake, UIStateControl uiStateControl, IPlaytestSessionIdentity identity)
        {
            _handshake = handshake;
            _uiStateControl = uiStateControl;
            _identity = identity;
        }

        public void Initialize()
        {
            // 記録するかの決定は AlwaysOnCaptureSetting が1つだけ持つ。持たないと調査用・テスト用の起動まで本番のProgressRecords/へ書き始める
            // AlwaysOnCaptureSetting holds the only decision on whether to record; without it even investigation and test boots write into the real ProgressRecords/
            if (!AlwaysOnCaptureSetting.Current.IsEnabled)
            {
                Debug.Log($"進行記録を開始しません: {AlwaysOnCaptureSetting.DisabledReason}");
                return;
            }
            StartSession();
        }

        // 記録を開始する唯一の口。常時記録を切ったまま記録だけ通したい起動はここを直接呼ぶ（WorldSnapshotRing.Start と同じ形）
        // The only entry point that starts recording; a boot that keeps capture off but still wants the record calls this directly (the same shape as WorldSnapshotRing.Start)
        public void StartSession()
        {
            if (_started)
            {
                Debug.LogWarning("進行記録は既に開始済みのため二重に開始しません");
                return;
            }
            _started = true;

            // ヘッダを先に同期で書き、その後で購読を張る。イベント追記は必ずヘッダ付きの current/ に入る
            // The header is written synchronously before any subscription, so appends always land in a current/ that has one
            _writer.WriteHeader(CreateHeader());
            ProgressWorldPlayTimeQuery.FillAsync(_writer, _sessionCancellation.Token).Forget(ProgressWorldPlayTimeQuery.LogFailure);

            _uiStateControl.OnStateChanged += OnUiStateChanged;
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(ResearchCompleteEventPacket.EventTag, OnResearchCompleted));
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnChallengeCompleted));
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(CraftCompletedEventPacket.EventTag, OnCraftCompleted));
            _eventSubscriptions.Add(ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockPlaced));

            // 終了理由は書き出しの前に流れてくる。初期化失敗の終了をプレイヤーが選んだ終了と同じ quit で閉じない
            // The reason arrives before the flush, so a fold-up after a failed initialization never closes as the player's own quit
            GameShutdownEvent.OnGameShutdown.Subscribe(reason => _shutdownReason = reason).AddTo(_eventSubscriptions);
            GameShutdownEvent.RegisterParticipant(this);

            #region Internal

            // 起動時点で確定する値だけでヘッダを組む。サーバー応答を待つ値は後から上書きする
            // Builds the header from the values fixed at boot; the ones awaiting the server are overwritten later
            ProgressRecordHeader CreateHeader()
            {
                var completedChallenges = _handshake.Challenges.SelectMany(category => category.CompletedChallenges).Select(challenge => challenge.ChallengeGuid);
                return new ProgressRecordHeader
                {
                    SteamId = _identity.SteamId,
                    BuildInfo = RepositoryStateProbe.ReadBuildInfo(),
                    SessionStart = ProgressUtcTime.ToIso(_sessionStartUtc),
                    BaselineChallenges = ProgressBaseline.CompletedChallengeGuids(completedChallenges),
                    BaselineResearch = ProgressBaseline.CompletedResearchGuids(_handshake.ResearchNodeStates),
                };
            }

            #endregion
        }

        public void Dispose()
        {
            _sessionCancellation.Cancel();
            _sessionCancellation.Dispose();
            if (_started) _uiStateControl.OnStateChanged -= OnUiStateChanged;
            _eventSubscriptions.Dispose();
            GameShutdownEvent.UnregisterParticipant(this);
            _writer.Dispose();
        }

        // 終了時に record.json を書く。書き出しは同期IOなので待ちは一瞬で終わる
        // Writes record.json at shutdown; the write is synchronous IO and finishes immediately
        public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            if (_writer.Closed) return UniTask.FromResult(ShutdownFlushResult.AlreadyShutdown);

            FlushPlacedBlocks();
            var closeResult = _writer.Close(ProgressEndReason.FromShutdownReason(_shutdownReason), DateTime.UtcNow);
            if (closeResult.BundleDirectory != null) return UniTask.FromResult(ShutdownFlushResult.Flushed);

            // 書けずに閉じられなかった終了を成功として畳むと、例外で落ちた参加者より軽い扱いになる。原因（ディスク）は同じなので同じ重さで返す
            // Folding an unwritable close into success would rank it below a participant that threw, yet the cause (the disk) is the same, so it returns the same weight
            if (closeResult.WriteFailed)
            {
                Debug.LogError("今回のセッションの進行記録を書き出せませんでした（書き出しに失敗。記録は current/ に残り次回起動で回収されます）");
                return UniTask.FromResult(ShutdownFlushResult.FlushFailed);
            }

            // 書く中身が1件も無かった。記録が1件出ない事実は黙って終わらせない
            // There was nothing to write; one missing record must never end in silence
            Debug.LogWarning("今回のセッションに書き出す進行記録がありませんでした（current/ が空）");
            return UniTask.FromResult(ShutdownFlushResult.NothingFlushed);
        }

        public void RecordReportSent(string kind)
        {
            if (!IsRecording(ProgressEventType.ReportSent)) return;
            _writer.Append(ProgressEvents.ReportSent(DateTime.UtcNow, GameUpdater.CurrentTick, kind));
        }

        // 記録を開始していない起動（常時記録オフのテスト・DSL・調査用）でプッシュを書くと、ヘッダの無い current/ が湧く
        // Pushing without a started session (a capture-off test, DSL or investigation boot) would conjure a headerless current/
        // その残骸は次回起動で「一度も遊んでいないセッション」として1件出荷されるため、理由を1度だけ出して捨てる
        // The next boot would ship that leftover as a session nobody ever played, so it is dropped with its reason logged once
        private bool IsRecording(string eventType)
        {
            if (_started) return true;
            if (_pushWithoutSessionLogged) return false;
            _pushWithoutSessionLogged = true;
            Debug.Log($"進行記録を開始していないためプッシュを記録しません type:{eventType}: {AlwaysOnCaptureSetting.DisabledReason}");
            return false;
        }

        private void OnUiStateChanged(UIStateEnum state)
        {
            // 区間の設置数を先に確定させてから遷移を書く。順序が逆だと「建築モードで設置したか」が次の区間へずれる
            // The interval's placements are settled before the transition; the reverse order would slide "placed while building" into the next interval
            FlushPlacedBlocks();
            _writer.Append(ProgressEvents.UiStateChanged(DateTime.UtcNow, GameUpdater.CurrentTick, state.ToString()));
        }

        private void OnResearchCompleted(byte[] payload)
        {
            _writer.Append(ProgressServerEvents.ResearchCompleted(payload, DateTime.UtcNow, GameUpdater.CurrentTick));
        }

        private void OnChallengeCompleted(byte[] payload)
        {
            _writer.Append(ProgressServerEvents.ChallengeCompleted(payload, DateTime.UtcNow, GameUpdater.CurrentTick));
        }

        private void OnCraftCompleted(byte[] payload)
        {
            _writer.Append(ProgressServerEvents.CraftCompleted(payload, DateTime.UtcNow, GameUpdater.CurrentTick));
        }

        // BlockIdはマスタのロード順で採番される揮発値。記録に残すと別ロードで別ブロックとして再生される
        // BlockId is volatile, renumbered per master load; recording it would replay as a different block on another load
        // 設置数だけが集計対象。1件ずつ行にすると設置のたびに追記が走るので、区間の合計だけを書く（ADR 0060 裁定9）
        // Only the count is aggregated; one line per placement would append on every block, so only the interval total is written (ADR 0060 adjudication 9)
        // 数えるのは「このワールドで置かれた数」で、テスター自身のぶんではない。設置イベントは設置者を載せずに全クライアントへ配られるため、協力プレイでは相方の設置も入る
        // The number counts placements in this world rather than this tester's own: the placement event carries no placer and reaches every client, so a co-op partner's blocks land here too
        private void OnBlockPlaced(byte[] payload)
        {
            _placedBlockCount++;
        }

        private void FlushPlacedBlocks()
        {
            if (_placedBlockCount == 0) return;
            _writer.Append(ProgressEvents.BlockPlaced(DateTime.UtcNow, GameUpdater.CurrentTick, _placedBlockCount));
            _placedBlockCount = 0;
        }
    }
}
