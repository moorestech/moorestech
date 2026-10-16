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
    /// - 走り方式の初期値配信
    /// - 走り方式の変化通知
    /// - Publishes the initial sprint mode
    /// - Publishes sprint mode changes
    /// </summary>
    public class SprintInputModeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "settings.sprintInputMode";

        private readonly IDisposable _modeSubscription;

        public SprintInputModeTopic(WebSocketHub hub)
        {
            _modeSubscription = SprintInputModePreference.OnSprintInputModeChanged.Subscribe(_ => hub.Publish(TopicName, BuildJson()));
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
