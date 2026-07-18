using System;
using Client.Skit.UI;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class SkitPresentationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "skit.presentation";
        private readonly WebSocketHub _hub;
        private readonly IDisposable _subscription;

        public SkitPresentationTopic(WebSocketHub hub)
        {
            _hub = hub;
            _subscription = SkitPresentationStateStore.Instance.OnChanged.Subscribe(Publish);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(WebUiJson.Serialize(SkitPresentationStateStore.Instance.Current));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish(SkitPresentationData data)
        {
            _hub.Publish(TopicName, WebUiJson.Serialize(data));
        }
    }
}
