using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.UI.UIState.State;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// ui_state.current トピック: 現在のUIStateを push（Web側画面ルーティングの正）
    /// ui_state.current topic: pushes the current UI state (source of truth for web-side routing)
    /// </summary>
    public class UiStateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui_state.current";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly UIStateDictionary _uiStateDictionary;
        private readonly TrainHUDScreenState _trainHudState;
        private readonly SkitState _skitState;
        private readonly IDisposable _trainStateSubscription;
        private readonly IDisposable _skitStateSubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public UiStateTopic(WebSocketHub hub, UIStateControl uiStateControl, UIStateDictionary uiStateDictionary, TrainHUDScreenState trainHudState, SkitState skitState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _uiStateDictionary = uiStateDictionary;
            _trainHudState = trainHudState;
            _skitState = skitState;

            // state遷移を購読して push する
            // Subscribe to state transitions and push them
            _uiStateControl.OnStateChanged += OnStateChanged;
            _trainStateSubscription = _trainHudState.OnPresentationChanged.Subscribe(_ => SchedulePublish());
            _skitStateSubscription = _skitState.OnPresentationChanged.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _trainStateSubscription.Dispose();
            _skitStateSubscription.Dispose();
        }

        private void OnStateChanged(UIStateEnum state)
        {
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 同フレーム多段遷移でもフレーム末の最終stateだけ配信する
        // INFRA-7 debounce rule: publish only the final state at frame end even on multi-hop transitions
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }

            #endregion
        }

        // 入れ子stateを持つ画面（列車HUD・スキット）だけsubStateを配る
        // Only screens with a nested state (train HUD, skit) carry a subState
        private string ResolveSubState(UIStateEnum currentState)
        {
            if (currentState == UIStateEnum.TrainHUDScreen) return _trainHudState.SubState.ToString();
            if (currentState == UIStateEnum.Story) return _skitState.SubState.ToString();
            return null;
        }
        
        private string BuildJson()
        {
            var currentState = _uiStateControl.CurrentState;

            // 現stateが自分で宣言したヒントをそのまま配る（内容の正はstate側・ADR-0032）
            // Publish the hints the current state declares for itself; the state owns the content (ADR-0032)
            var keyHints = _uiStateDictionary.GetState(currentState).GetKeyHints()
                .Select(hint => new KeyHintDto { KeyNameKey = hint.KeyNameKey.Key, TextKey = hint.TextKey.Key })
                .ToArray();

            return WebUiJson.Serialize(new UiStateDto
            {
                State = currentState.ToString(),
                SubState = ResolveSubState(currentState),
                KeyHints = keyHints,
            });
        }
    }

    /// <summary>
    /// ui_state.current の配信 DTO
    /// Payload DTO for ui_state.current
    /// </summary>
    public class UiStateDto
    {
        public string State;
        public string SubState;
        public KeyHintDto[] KeyHints;
    }

    /// <summary>
    /// 操作ヒント1件の配信 DTO。キー名も文言もローカライズキーで運ぶ
    /// Payload DTO for one key hint; both the key name and the text travel as localization keys
    /// </summary>
    public class KeyHintDto
    {
        public string KeyNameKey;
        public string TextKey;
    }
}
