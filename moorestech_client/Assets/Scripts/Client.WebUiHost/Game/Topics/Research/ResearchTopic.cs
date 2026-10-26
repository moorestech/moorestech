using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Research;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 研究マスタと状態を合成して配信
    /// Merges research master and states and pushes them
    /// </summary>
    public class ResearchTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "research.tree";

        private readonly WebSocketHub _hub;
        private readonly ResearchNodeStateStore _store;
        private readonly IDisposable _subscription;

        public ResearchTopic(WebSocketHub hub, ResearchNodeStateStore store)
        {
            _hub = hub;
            _store = store;

            // 差し替えごとに全量を同期配信
            // Publish the full tree synchronously per replacement
            _subscription = _store.OnNodeStatesChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            var dto = new ResearchTreeDto { Nodes = new List<ResearchNodeDto>() };
            foreach (var master in MasterHolder.ResearchMaster.GetAllResearches())
            {
                dto.Nodes.Add(ResearchNodeDtoFactory.Create(master, _store.NodeStates));
            }
            return WebUiJson.Serialize(dto);
        }
    }
}
