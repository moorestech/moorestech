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

        // 変更は溜めてFlushで吐く。1設置あたり2通・ドラッグでセル数分に増幅させないため
        // Changes accumulate and leave on Flush, so one placement never emits two notifications nor a drag one per cell
        private readonly HashSet<(int playerId, BlockId walletBlockId)> _dirtyWallets = new();

        // プレイヤー束縛済みreaderは使い回す（設置1セルごとに作らない）
        // Player-bound readers are reused so a drag never allocates one per cell
        private readonly Dictionary<int, IRemainingPlacementCountReader> _readers = new();

        // 生のBlockIdを受け取り財布キーへの正規化は内側で行う（クライアント側と同一契約）
        // Takes a raw BlockId and normalizes it to the wallet key inside, the same contract as the client side
        public int GetRemainingCount(int playerId, BlockId blockId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return 0;
            return wallets.TryGetValue(ConstructionWalletUtil.ResolveWalletBlockId(blockId), out var remaining) ? remaining : 0;
        }

        public IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return Array.Empty<(BlockId, int)>();
            return wallets.Where(pair => 0 < pair.Value).Select(pair => (pair.Key, pair.Value)).ToList();
        }

        public IRemainingPlacementCountReader GetReader(int playerId)
        {
            if (_readers.TryGetValue(playerId, out var reader)) return reader;
            reader = new PlayerBoundReader(this, playerId);
            _readers[playerId] = reader;
            return reader;
        }

        public void ConsumeOne(int playerId, BlockId walletBlockId)
        {
            var remaining = GetRemainingCount(playerId, walletBlockId);
            if (remaining <= 0) throw new InvalidOperationException($"Wallet is empty. playerId:{playerId} walletBlockId:{walletBlockId.AsPrimitive()}");
            Set(playerId, walletBlockId, remaining - 1);
        }

        public void Refill(int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            Set(playerId, walletBlockId, GetRemainingCount(playerId, walletBlockId) + placementsPerCost);
        }

        public void ApplyReturn(int playerId, BlockId walletBlockId, bool condensed)
        {
            // Nに達した分は素材へ凝縮し財布から消える
            // The portion that reached one set's worth condenses into materials and leaves the wallet
            Set(playerId, walletBlockId, condensed ? 0 : GetRemainingCount(playerId, walletBlockId) + 1);
        }

        public void FlushChanges()
        {
            foreach (var (playerId, walletBlockId) in _dirtyWallets)
            {
                _onRemainingCountChanged.OnNext(new RemainingPlacementCountChange(playerId, walletBlockId, GetRemainingCount(playerId, walletBlockId)));
            }
            _dirtyWallets.Clear();
        }

        public List<PlayerRemainingPlacementCountSaveJsonObject> GetSaveJsonObject()
        {
            return _remainingCounts
                .Select(player => new PlayerRemainingPlacementCountSaveJsonObject(player.Key, player.Value
                    .Where(wallet => 0 < wallet.Value)
                    .Select(wallet => new RemainingPlacementCountEntrySaveJsonObject(MasterHolder.BlockMaster.GetBlockMaster(wallet.Key).BlockGuid.ToString(), wallet.Value))
                    .ToList()))
                .Where(player => 0 < player.Entries.Count)
                .ToList();
        }

        public void LoadRemainingCounts(List<PlayerRemainingPlacementCountSaveJsonObject> saveData)
        {
            _remainingCounts.Clear();
            _dirtyWallets.Clear();
            foreach (var player in saveData)
            {
                foreach (var entry in player.Entries) AddLoadedEntry(player.PlayerId, entry);
                ClampLoadedWallets(player.PlayerId);
            }

            #region Internal

            void AddLoadedEntry(int playerId, RemainingPlacementCountEntrySaveJsonObject entry)
            {
                // マスタから消えたブロックの財布は捨てる（形状不正で全体を落とさない）
                // Drop wallets whose block vanished from the master so a stale save never aborts the load
                if (!Guid.TryParse(entry.BlockGuid, out var blockGuid)) return;
                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
                if (blockId == null) return;

                // 財布を使わないブロックと定義域(0 < count < N)の外は破損値として捨てる
                // Blocks that never use the wallet, and counts outside the domain (0 < count < N), are corrupt values and get dropped
                var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId.Value);
                var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(walletBlockId).PlacementsPerCost;
                if (!ConstructionWalletUtil.UsesWallet(placementsPerCost)) return;
                if (entry.Count <= 0 || placementsPerCost <= entry.Count) return;

                // 正規化で同じ財布へ重なったキーは合算する
                // Keys that collapse onto the same wallet after normalization are summed
                var wallets = GetOrCreate(playerId);
                wallets[walletBlockId] = wallets.TryGetValue(walletBlockId, out var current) ? current + entry.Count : entry.Count;
            }

            void ClampLoadedWallets(int playerId)
            {
                // 合算でNに達した財布は1セット分が素材へ凝縮済みのはずなので定義域内へ丸める
                // A summed wallet that reached one set's worth should already have condensed into materials, so clamp it back into the domain
                if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return;
                foreach (var walletBlockId in wallets.Keys.ToList())
                {
                    var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(walletBlockId).PlacementsPerCost;
                    if (placementsPerCost <= wallets[walletBlockId]) wallets[walletBlockId] = placementsPerCost - 1;
                }
            }

            #endregion
        }

        private void Set(int playerId, BlockId blockId, int remaining)
        {
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
            GetOrCreate(playerId)[walletBlockId] = remaining;
            _dirtyWallets.Add((playerId, walletBlockId));
        }

        private Dictionary<BlockId, int> GetOrCreate(int playerId)
        {
            if (_remainingCounts.TryGetValue(playerId, out var wallets)) return wallets;
            wallets = new Dictionary<BlockId, int>();
            _remainingCounts[playerId] = wallets;
            return wallets;
        }

        // 1プレイヤー分へ束縛した読み取り口。問い合わせ側はplayerIdを持たなくてよくなる
        // A read port bound to one player, so the query side never has to carry a playerId
        private class PlayerBoundReader : IRemainingPlacementCountReader
        {
            public IObservable<Unit> OnWalletChanged { get; }

            private readonly RemainingPlacementCountDataStore _store;
            private readonly int _playerId;

            public PlayerBoundReader(RemainingPlacementCountDataStore store, int playerId)
            {
                _store = store;
                _playerId = playerId;
                OnWalletChanged = store.OnRemainingCountChanged.Where(change => change.PlayerId == playerId).AsUnitObservable();
            }

            public int GetRemainingCount(BlockId blockId)
            {
                return _store.GetRemainingCount(_playerId, blockId);
            }
        }
    }
}
