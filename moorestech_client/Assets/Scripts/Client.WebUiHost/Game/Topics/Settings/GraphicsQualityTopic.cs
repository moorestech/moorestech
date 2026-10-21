using System;
using Client.Game.InGame.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// - 画質設定の初期値配信
    /// - 画質設定の変化通知
    /// - Publishes the initial graphics quality
    /// - Publishes graphics quality changes
    /// </summary>
    public class GraphicsQualityTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "settings.graphicsQuality";

        private readonly IDisposable _settingsSubscription;

        public GraphicsQualityTopic(WebSocketHub hub)
        {
            _settingsSubscription = GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _settingsSubscription.Dispose();
        }

        private static string BuildJson()
        {
            var settings = GraphicsQualityPreference.Current;
            return WebUiJson.Serialize(new GraphicsQualityData
            {
                Preset = GraphicsQualityWire.PresetToWire(settings),
                Settings = GraphicsQualityWire.ToData(settings),
                RecoveredInvalidPreference = GraphicsQualityPreference.RecoveredInvalidPreference,
            });
        }

        private class GraphicsQualityData
        {
            public string Preset;
            public bool RecoveredInvalidPreference;
            public GraphicsQualitySettingsData Settings;
        }
    }
}
