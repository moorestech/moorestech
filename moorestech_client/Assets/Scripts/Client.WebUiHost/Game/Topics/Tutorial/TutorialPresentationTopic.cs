using System;
using Client.Game.InGame.Tutorial;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class TutorialPresentationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "tutorial.presentation";
        private readonly WebSocketHub _hub;
        private readonly TutorialPresentationStateStore _store;
        private readonly IDisposable _subscription;

        public TutorialPresentationTopic(WebSocketHub hub)
        {
            _hub = hub;
            _store = TutorialPresentationStateStore.Instance;
            _subscription = _store.ObserveChanged().Subscribe(Publish);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(WebUiJson.Serialize(_store.GetCurrent()));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish(TutorialPresentationData data)
        {
            _hub.Publish(TopicName, WebUiJson.Serialize(data));
        }
    }
}
