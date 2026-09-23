using System;
using System.Linq;
using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
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

        // 記録するかの決定は AlwaysOnCaptureSetting が1つだけ持つ。持たないと無人起動（テスト・プレイ録画テスト）まで本番のProgressRecords/へ書き始める
        // AlwaysOnCaptureSetting holds the only decision on whether to record; without it even unattended boots (tests, recorded playtests) write into the real ProgressRecords/
        public void Initialize()
        {
            if (!AlwaysOnCaptureSetting.Current.IsEnabled)
            {
                Debug.Log($"進行記録を開始しません: {AlwaysOnCaptureSetting.DisabledReason}");
                return;
            }
            StartSession();
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
            var endReason = _shutdownReason == GameShutdownReason.InitializationFailed ? ProgressEndReason.InitializationFailed : ProgressEndReason.Quit;
            var closeResult = _writer.Close(endReason, DateTime.UtcNow);
            if (closeResult.BundleDirectory != null) return UniTask.FromResult(ShutdownFlushResult.Flushed);

            // 書けずに閉じられなかった終了を成功として畳むと、例外で落ちた参加者より軽い扱いになる。原因（ディスク）は同じなので同じ重さで返す
            // Folding an unwritable close into success would rank it below a participant that threw, yet the cause (the disk) is the same, so it returns the same weight
            if (closeResult.WriteFailed)
            {
                Debug.LogError("今回のセッションの進行記録を書き出せませんでした（書き出しに失敗。記録は current/ に残り次回起動で回収されます）");
                return UniTask.FromResult(ShutdownFlushResult.FlushFailed);
            }

            Debug.LogWarning("今回のセッションに書き出す進行記録がありませんでした（current/ が空）");
            return UniTask.FromResult(ShutdownFlushResult.NothingFlushed);
        }

        public void RecordReportSent(PlaytestReportKind kind)
        {
            if (!IsRecording(ReportSentEvent.TypeName)) return;
            _writer.Append(new ReportSentEvent(DateTime.UtcNow, GameUpdater.CurrentTick, kind));
        }

        private void StartSession()
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

            // 数えるのは本人の設置確定だけ。サーバーの設置イベントは設置者を載せず全クライアントへ配られ、協力プレイの相方の設置まで入る（F16）
            // Only this player's confirmed placements count; the server's placement event carries no placer and reaches every client, co-op partners included (F16)
            PlaceBlockProtocolSender.OnPlaceBlockSent.Subscribe(OnBlockPlaced).AddTo(_eventSubscriptions);

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
                var header = new ProgressRecordHeader
                {
                    SteamId = string.IsNullOrEmpty(_identity.SteamId) ? null : _identity.SteamId,
                    BuildInfo = RepositoryStateProbe.ReadBuildInfo(),
                    SessionStart = ProgressUtcTime.ToIso(_sessionStartUtc),
                    BaselineChallenges = ProgressBaseline.CompletedChallengeGuids(completedChallenges),
                    BaselineResearch = ProgressBaseline.CompletedResearchGuids(_handshake.ResearchNodeStates),
                };
                if (header.SteamId == null) header.AddMissing("steamId", "テスター識別（SteamID）が差し込まれていない（plan D 未導入またはSteam未起動）");
                return header;
            }

            #endregion
        }

        // 記録を開始していない起動でプッシュを書くと、ヘッダの無い current/ が湧いて次回起動が偽の記録を1件出荷する。理由を1度だけ出して捨てる
        // Pushing without a started session conjures a headerless current/ that the next boot ships as a bogus record, so it is dropped with its reason logged once
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
            _writer.Append(new UiStateChangedEvent(DateTime.UtcNow, GameUpdater.CurrentTick, state.ToString()));
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

        // 設置数だけが集計対象。1件ずつ行にすると設置のたびに追記が走るので、区間の合計だけを書く（ADR 0060 裁定9）
        // Only the count is aggregated; one line per placement would append on every block, so only the interval total is written (ADR 0060 adjudication 9)
        private void OnBlockPlaced(int placedCellCount)
        {
            _placedBlockCount += placedCellCount;
        }

        private void FlushPlacedBlocks()
        {
            if (_placedBlockCount == 0) return;
            _writer.Append(new BlockPlacedEvent(DateTime.UtcNow, GameUpdater.CurrentTick, _placedBlockCount));
            _placedBlockCount = 0;
        }
    }
}
