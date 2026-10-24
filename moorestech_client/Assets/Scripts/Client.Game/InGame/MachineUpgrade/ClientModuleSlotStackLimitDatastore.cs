using System;
using Game.Block.Interface.MachineUpgrade;
using UniRx;

namespace Client.Game.InGame.MachineUpgrade
{
    /// <summary>
    ///     サーバーのモジュールスロット上限のクライアント側ミラー。初期データと購読で更新する
    ///     Client-side mirror of the server's module-slot stack limit, updated from initial data and subscriptions
    /// </summary>
    public class ClientModuleSlotStackLimitDatastore : IModuleSlotStackLimitLookup, IClientModuleSlotStackLimitMutation
    {
        public int CurrentLevel { get; private set; }
        public int CurrentStackLimit => ModuleSlotStackLimitMasterUtil.GetStackLimit(CurrentLevel);
        public IObservable<int> OnStackLimitChanged => _onStackLimitChanged;

        private readonly Subject<int> _onStackLimitChanged = new();

        void IClientModuleSlotStackLimitMutation.Apply(int level)
        {
            // 不正な受信値で現在の状態を壊さない
            // Reject invalid received levels before changing current state
            if (level < 0 || ModuleSlotStackLimitMasterUtil.GetMaxLevel() < level)
            {
                UnityEngine.Debug.LogWarning($"Invalid module slot stack level: {level}");
                return;
            }
            if (level == CurrentLevel) return;
            CurrentLevel = level;
            _onStackLimitChanged.OnNext(CurrentStackLimit);
        }
    }
}
