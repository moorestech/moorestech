using System;
using Client.Game.InGame.Mining;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class MiningHudTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.mining_hud";
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);

        private readonly WebSocketHub _hub;
        private readonly MapObjectMiningController _controller;
        private readonly IDisposable _subscription;
        private string _lastJson;

        public MiningHudTopic(WebSocketHub hub, MapObjectMiningController controller)
        {
            _hub = hub;
            _controller = controller;

            // 連続進捗を100ms間隔でサンプリングし、同値payloadは送らない
            // Sample continuous progress every 100ms and suppress identical payloads
            _subscription = Observable.Interval(SampleInterval).Subscribe(_ => PublishIfChanged());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();

        private void PublishIfChanged()
        {
            var json = BuildJson();
            if (json == _lastJson) return;
            _lastJson = json;
            _hub.Publish(TopicName, json);
        }

        private string BuildJson()
        {
            var targetName = _controller.GetFocusTargetName();
            return WebUiJson.Serialize(new MiningHudDto
            {
                Visible = targetName.Length > 0,
                TargetName = targetName,
                Mining = _controller.IsMining(),
                Progress = _controller.GetMiningProgress(),
            });
        }
    }

    public class MiningHudDto
    {
        public bool Visible;
        public string TargetName;
        public bool Mining;
        public float Progress;
    }
}
