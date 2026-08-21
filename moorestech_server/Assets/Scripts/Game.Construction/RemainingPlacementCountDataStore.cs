using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    /// <summary>
    /// プレイヤー×財布の残り設置数。読み取りではレコードを作らず、0件はセーブしない（前例 HotbarAssignmentDatastore）
    /// Remaining placements per player x wallet; reads never create records and zero entries are not saved (precedent: HotbarAssignmentDatastore)
    /// </summary>
    public class RemainingPlacementCountDataStore : IRemainingPlacementCountLookup, IRemainingPlacementCountMutation
    {
        public IObservable<RemainingPlacementCountChange> OnRemainingCountChanged => _onRemainingCountChanged;
        private readonly Subject<RemainingPlacementCountChange> _onRemainingCountChanged = new();

        private readonly Dictionary<int, Dictionary<BlockId, int>> _remainingCounts = new();

        public int GetRemainingCount(int playerId, BlockId walletBlockId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return 0;
            return wallets.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return Array.Empty<(BlockId, int)>();
            return wallets.Where(pair => pair.Value > 0).Select(pair => (pair.Key, pair.Value)).ToList();
        }

        public bool TryConsumeOne(int playerId, BlockId walletBlockId)
        {
            var remaining = GetRemainingCount(playerId, walletBlockId);
            if (remaining <= 0) return false;
            Set(playerId, walletBlockId, remaining - 1);
            return true;
        }

        public void Refill(int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            Set(playerId, walletBlockId, GetRemainingCount(playerId, walletBlockId) + placementsPerCost);
        }

        public bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            var returned = GetRemainingCount(playerId, walletBlockId) + 1;
            // 設置数/1セットに達した分は素材へ凝縮されるので財布からは消える
            // Reaching one set's worth condenses into materials, so it leaves the wallet
            var condensed = placementsPerCost <= returned;
            Set(playerId, walletBlockId, condensed ? 0 : returned);
            return condensed;
        }

        public List<PlayerRemainingPlacementCountSaveJsonObject> GetSaveJsonObject()
        {
            return _remainingCounts
                .Select(player => new PlayerRemainingPlacementCountSaveJsonObject(player.Key, player.Value
                    .Where(wallet => wallet.Value > 0)
                    .Select(wallet => new RemainingPlacementCountEntrySaveJsonObject(MasterHolder.BlockMaster.GetBlockMaster(wallet.Key).BlockGuid.ToString(), wallet.Value))
                    .ToList()))
                .Where(player => player.Entries.Count > 0)
                .ToList();
        }

        public void LoadRemainingCounts(List<PlayerRemainingPlacementCountSaveJsonObject> saveData)
        {
            _remainingCounts.Clear();
            foreach (var player in saveData)
            {
                foreach (var entry in player.Entries)
                {
                    // マスタから消えたブロックの財布は捨てる（形状不正で全体を落とさない）
                    // Drop wallets whose block vanished from the master so a stale save never aborts the load
                    if (!Guid.TryParse(entry.BlockGuid, out var blockGuid)) continue;
                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
                    if (blockId == null || entry.Count <= 0) continue;
                    GetOrCreate(player.PlayerId)[blockId.Value] = entry.Count;
                }
            }
        }

        private void Set(int playerId, BlockId walletBlockId, int remaining)
        {
            GetOrCreate(playerId)[walletBlockId] = remaining;
            _onRemainingCountChanged.OnNext(new RemainingPlacementCountChange(playerId, walletBlockId, remaining));
        }

        private Dictionary<BlockId, int> GetOrCreate(int playerId)
        {
            if (_remainingCounts.TryGetValue(playerId, out var wallets)) return wallets;
            wallets = new Dictionary<BlockId, int>();
            _remainingCounts[playerId] = wallets;
            return wallets;
        }
    }
}
