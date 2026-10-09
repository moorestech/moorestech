using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーンポール1本が持つチェーン接続の台帳。dirty化と状態通知は持ち主のコンポーネントが行う
    /// Ledger of a pole's chain connections; topology dirtying and state notifications stay with the owning component
    /// </summary>
    public class GearChainConnectionSet : IGearChainConnectionLookup, IGearChainConnectionMutation
    {
        private readonly Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> _targets = new();

        public int Count => _targets.Count;
        public IEnumerable<BlockInstanceId> PartnerIds => _targets.Keys;
        public IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> Targets => _targets;

        public bool Contains(BlockInstanceId partnerId)
        {
            return _targets.ContainsKey(partnerId);
        }

        public bool TryGetRecord(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            var found = _targets.TryGetValue(partnerId, out var connection);
            record = found ? connection.Record : default;
            return found;
        }

        public void Add(BlockInstanceId partnerId, IGearEnergyTransformer transformer, ConnectionLineRecord record)
        {
            _targets.Add(partnerId, (transformer, record));
        }

        public bool TryRemove(BlockInstanceId partnerId, out ConnectionLineRecord record)
        {
            if (!_targets.Remove(partnerId, out var connection))
            {
                record = default;
                return false;
            }

            record = connection.Record;
            return true;
        }

        public void Clear()
        {
            _targets.Clear();
        }

        // 撤去時に返す素材を接続ごとに展開する（展開規則は接続線の正本）
        // Expand the materials to refund on removal, per connection (the rule lives in the connection-line definition)
        public IReadOnlyList<IItemStack> CreateRefundItems()
        {
            var refundItems = new List<IItemStack>();
            foreach (var connection in _targets.Values) refundItems.AddRange(ConnectionLineRefundItems.Create(connection.Record.Materials));
            return refundItems;
        }

        public GearChainPoleSaveDataJsonObject CreateSaveData()
        {
            return new GearChainPoleSaveDataJsonObject(_targets);
        }
    }
}
