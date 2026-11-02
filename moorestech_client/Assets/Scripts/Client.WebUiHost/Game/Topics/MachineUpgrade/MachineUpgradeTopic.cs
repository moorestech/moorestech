using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.MachineUpgrade;
using UniRx;

namespace Client.WebUiHost.Game.Topics.MachineUpgrade
{
    /// <summary>
    ///     machine_upgrade.current トピック: モジュールスロット1つあたりの上限。クライアントのミラーの変化で再配信
    ///     machine_upgrade.current topic: the per-module-slot stack limit, republished when the client mirror changes
    /// </summary>
    public class MachineUpgradeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "machine_upgrade.current";

        private readonly WebSocketHub _hub;
        private readonly IModuleSlotStackLimitLookup _stackLimitLookup;
        private readonly IDisposable _subscription;

        public MachineUpgradeTopic(WebSocketHub hub, IModuleSlotStackLimitLookup stackLimitLookup)
        {
            _hub = hub;
            _stackLimitLookup = stackLimitLookup;

            // ミラーは値を書き換えてから通知するので、ここでは変化後の上限を読める
            // The mirror notifies after updating, so the changed limit is visible here
            _subscription = _stackLimitLookup.OnStackLimitChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
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
            return WebUiJson.Serialize(new MachineUpgradeDto { ModuleSlotStackLimit = _stackLimitLookup.CurrentStackLimit });
        }
    }

    public class MachineUpgradeDto
    {
        public int ModuleSlotStackLimit;
    }
}
