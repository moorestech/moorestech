using System;
using Game.Block.Interface.MachineUpgrade;
using UniRx;

namespace Client.Game.InGame.MachineUpgrade
{
    /// <summary>
    ///     サーバーのモジュールスロット上限のクライアント側ミラー。初期データと購読で更新する
    ///     Client-side mirror of the server's module-slot stack limit, updated from initial data and subscriptions
    /// </summary>
    public class ClientModuleSlotStackLimitDatastore : IModuleSlotStackLimitLookup
    {
        public int CurrentLevel { get; private set; }
        public int CurrentStackLimit => ModuleSlotStackLimitMasterUtil.GetStackLimit(CurrentLevel);
        public IObservable<int> OnStackLimitChanged => _onStackLimitChanged;

        private readonly Subject<int> _onStackLimitChanged = new();

        public void Apply(int level)
        {
            if (level == CurrentLevel) return;
            CurrentLevel = level;
            _onStackLimitChanged.OnNext(CurrentStackLimit);
        }
    }
}
