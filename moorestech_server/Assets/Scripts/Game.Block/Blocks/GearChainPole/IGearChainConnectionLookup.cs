using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーン接続台帳の読み取り面。書き換えは IGearChainConnectionMutation だけが行う
    /// Read surface of the chain connection ledger; only IGearChainConnectionMutation writes
    /// </summary>
    public interface IGearChainConnectionLookup
    {
        int Count { get; }
        IEnumerable<BlockInstanceId> PartnerIds { get; }
        IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> Targets { get; }
        bool Contains(BlockInstanceId partnerId);
        bool TryGetRecord(BlockInstanceId partnerId, out ConnectionLineRecord record);
        IReadOnlyList<IItemStack> CreateRefundItems();
        GearChainPoleSaveDataJsonObject CreateSaveData();
    }
}
