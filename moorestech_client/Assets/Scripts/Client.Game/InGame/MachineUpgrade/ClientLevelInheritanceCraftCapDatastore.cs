using System;
using System.Collections.Generic;
using Game.Block.Blocks.Machine.Upgrade;
using Game.Block.Interface.MachineUpgrade;
using UniRx;

namespace Client.Game.InGame.MachineUpgrade
{
    /// <summary>
    ///     サーバーの製作上限のクライアント側ミラー。初期データと購読で更新する
    ///     Client-side mirror of the server's craft caps, updated from initial data and subscriptions
    /// </summary>
    public class ClientLevelInheritanceCraftCapDatastore : ILevelInheritanceCraftCapLookup, IClientLevelInheritanceCraftCapMutation
    {
        public IReadOnlyDictionary<Guid, int> UnlockedCaps => _capTable.UnlockedCaps;
        public IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked => _onCraftCapUnlocked;

        private readonly Subject<(Guid familyBaseItemGuid, int level)> _onCraftCapUnlocked = new();

        // 「未登録は初期値・上がるだけ」はサーバーと同じ表で持つ
        // "Absent means initial, only rises" lives in the same table as the server
        private readonly LevelInheritanceCraftCapTable _capTable = new();

        public int GetCraftCap(Guid familyBaseItemGuid)
        {
            return _capTable.GetCraftCap(familyBaseItemGuid);
        }

        void IClientLevelInheritanceCraftCapMutation.Apply(Guid familyBaseItemGuid, int level)
        {
            if (!_capTable.TryRaise(familyBaseItemGuid, level)) return;
            _onCraftCapUnlocked.OnNext((familyBaseItemGuid, level));
        }
    }
}
