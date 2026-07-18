using System;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Skit.UI
{
    public class SkitPresentationStateStore : ISkitAutoAdvanceSink
    {
        private const int TypewriterIntervalMs = 50;
        private readonly Subject<SkitPresentationData> _onChanged = new();
        private readonly SkitIntentWaitController _waitController = new();
        private SkitPresentationData _current = SkitPresentationData.CreateNone("", 0);
        private ISkitActionController _actionController;
        public static readonly SkitPresentationStateStore Instance = new();
        public IObservable<SkitPresentationData> ObserveChanged()
        {
            return _onChanged;
        }
        public SkitPresentationData GetCurrent()
        {
            return _current;
        }
        // session境界では未完了waitを破棄し、再接続可能な新UUIDを発行する
        // At a session boundary, cancel unfinished waits and issue a new reconnect-safe UUID
        public void BeginBackground()
        {
            _waitController.Cancel();
            _current = SkitPresentationData.CreateBackground(Guid.NewGuid().ToString(), 0, "", "");
            Publish();
        }
        public void SetBackgroundText(string speakerName, string body)
        {
            _current = SkitPresentationData.CreateBackground(
                _current.SessionId, _current.SceneRevision + 1, speakerName, body);
            Publish();
        }
        // 通常スキットの操作状態は既存controllerを唯一の正としてstoreへ投影する
        // Project blocking controls into the store while retaining the existing controller as authority
        public void BeginBlocking(ISkitActionController actionController)
        {
            _waitController.Cancel();
            _actionController = actionController;
            _waitController.Bind(actionController, this);
            _actionController.SetSkip(false);
            _current = SkitPresentationData.CreateBlocking(
                Guid.NewGuid().ToString(), 0, "", "", Array.Empty<SkitChoice>(), false, false,
                actionController.IsAuto, false, false, "instant", 0, Array.Empty<string>());
            Publish();
        }
        // Actionが即着しても解放を失わないよう、waitをsnapshot配信前に作る
        // Create the wait before publishing so an immediately arriving Action cannot be lost
        public void PresentBlockingText(string speakerName, string body)
        {
            _waitController.StartAdvanceWait();
            _current = SkitPresentationData.CreateBlocking(
                _current.SessionId, _current.SceneRevision + 1, speakerName, body, Array.Empty<SkitChoice>(), true,
                _current.PresentationState.TransitionVisible, _actionController.IsAuto, _actionController.IsSkip,
                _current.PresentationState.UiHidden, "typewriter", TypewriterIntervalMs,
                new[] { "advance", "set-auto", "skip", "set-ui-hidden" });
            Publish();
            _waitController.ResetAutoAdvanceTimer();
        }
        public UniTask WaitForAdvanceAsync()
        {
            return _waitController.WaitForAdvanceAsync();
        }
        // choiceIdと表示順を同じsnapshotに固定し、jump情報はUnity内に留める
        // Freeze choice IDs with display order in one snapshot while keeping jumps inside Unity
        public void PresentChoices(SkitChoice[] choices)
        {
            _waitController.StartSelectionWait();
            _current = SkitPresentationData.CreateBlocking(
                _current.SessionId, _current.SceneRevision + 1, _current.PresentationState.SpeakerName,
                _current.PresentationState.Body, choices, _current.PresentationState.TextAreaVisible,
                _current.PresentationState.TransitionVisible, _actionController.IsAuto, _actionController.IsSkip,
                _current.PresentationState.UiHidden, "instant", 0,
                new[] { "select", "set-auto", "skip", "set-ui-hidden" });
            Publish();
        }
        public UniTask<string> WaitForSelectionAsync()
        {
            return _waitController.WaitForSelectionAsync();
        }
        // 全intentは共通のsession・revision・許可判定を通してから副作用を起こす
        // Route every intent through shared session, revision, and allowlist validation before side effects
        public SkitIntentResult TryAdvance(string sessionId, int revision)
        {
            var validation = Validate(sessionId, revision, "advance");
            if (!validation.Ok) return validation;
            CompleteAdvance();
            return SkitIntentResult.Success();
        }
        public SkitIntentResult TrySelect(string sessionId, int revision, string choiceId)
        {
            var validation = Validate(sessionId, revision, "select");
            if (!validation.Ok) return validation;
            if (SkitChoiceJumpResolver.ResolveSelectedIndex(choiceId, _current.PresentationState.Choices) < 0)
                return SkitIntentResult.Fail("unknown_choice");

            _current = _current.CopyWithChoices(
                _current.SceneRevision + 1, Array.Empty<SkitChoice>(), Array.Empty<string>());
            Publish();
            _waitController.CompleteSelection(choiceId);
            return SkitIntentResult.Success();
        }
        public SkitIntentResult TrySetAuto(string sessionId, int revision, bool enabled)
        {
            var validation = Validate(sessionId, revision, "set-auto");
            if (!validation.Ok) return validation;
            _actionController.SetAuto(enabled);
            PublishControlState(enabled, _actionController.IsSkip, _current.PresentationState.UiHidden);
            _waitController.ResetAutoAdvanceTimer();
            return SkitIntentResult.Success();
        }
        public SkitIntentResult TrySkip(string sessionId, int revision)
        {
            var validation = Validate(sessionId, revision, "skip");
            if (!validation.Ok) return validation;
            _actionController.SetSkip(true);
            if (_waitController.IsWaitingForAdvance())
            {
                CompleteAdvance();
            }
            else
            {
                PublishControlState(_actionController.IsAuto, true, _current.PresentationState.UiHidden);
            }
            return SkitIntentResult.Success();
        }
        public SkitIntentResult TrySetUiHidden(string sessionId, int revision, bool hidden)
        {
            var validation = Validate(sessionId, revision, "set-ui-hidden");
            if (!validation.Ok) return validation;
            PublishControlState(_actionController.IsAuto, _actionController.IsSkip, hidden);
            return SkitIntentResult.Success();
        }
        // Unity側screen commandも完全snapshotへ統合し、Web表示を復元可能にする
        // Fold Unity screen commands into the complete snapshot so the Web view remains restorable
        public void SetTextAreaVisible(bool visible)
        {
            PublishScreenState(visible, _current.PresentationState.TransitionVisible);
        }
        public void SetTransitionVisible(bool visible)
        {
            PublishScreenState(_current.PresentationState.TextAreaVisible, visible);
        }
        public void End()
        {
            _waitController.Cancel();
            _current = SkitPresentationData.CreateNone(_current.SessionId, _current.SceneRevision + 1);
            _actionController = null;
            Publish();
        }

        private SkitIntentResult Validate(string sessionId, int revision, string intent)
        {
            if (sessionId != _current.SessionId) return SkitIntentResult.Fail("stale_session");
            if (revision != _current.SceneRevision) return SkitIntentResult.Fail("stale_revision");
            if (Array.IndexOf(_current.AllowedIntents, intent) < 0) return SkitIntentResult.Fail("intent_not_allowed");
            return SkitIntentResult.Success();
        }
        private void CompleteAdvance()
        {
            // wait解放前にrevisionを進め、同一Actionの再送をstaleにする
            // Advance the revision before releasing the wait so a replayed Action becomes stale
            AdvanceRevisionAfterWait();
            _waitController.CompleteAdvance();
        }

        private void AdvanceRevisionAfterWait()
        {
            _current = _current.CopyWith(_current.SceneRevision + 1, Array.Empty<string>());
            Publish();
        }

        private void PublishControlState(bool autoEnabled, bool skipActive, bool uiHidden)
        {
            _current = _current.CopyWithControls(
                _current.SceneRevision + 1, autoEnabled, skipActive, uiHidden);
            Publish();
        }

        private void PublishScreenState(bool textAreaVisible, bool transitionVisible)
        {
            _current = _current.CopyWithScreen(
                _current.SceneRevision + 1, textAreaVisible, transitionVisible);
            Publish();
        }
        void ISkitAutoAdvanceSink.CompleteAutoAdvance()
        {
            if (_waitController.IsWaitingForAdvance()) CompleteAdvance();
        }

        private void Publish()
        {
            _onChanged.OnNext(_current);
        }
    }
}
