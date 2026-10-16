using System;
using Client.Input;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 走りの操作方式を snapshot と変化時 event で配信する
    /// Publishes the sprint input mode as a snapshot and on each change
    /// </summary>
    public class SprintInputModeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "settings.sprintInputMode";

        private readonly IDisposable _modeSubscription;

        public SprintInputModeTopic(WebSocketHub hub)
        {
            _modeSubscription = SprintInputModePreference.OnChanged.Subscribe(_ => hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _modeSubscription.Dispose();
        }

        private static string BuildJson()
        {
            return WebUiJson.Serialize(new SprintInputModeData { Mode = SprintInputModeWire.ToWire(SprintInputModePreference.Current) });
        }

        private class SprintInputModeData
        {
            public string Mode;
        }
    }
}
