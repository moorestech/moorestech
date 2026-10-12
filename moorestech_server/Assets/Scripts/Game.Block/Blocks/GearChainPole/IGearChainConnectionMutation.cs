using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーン接続台帳の変更面。持ち主のコンポーネントだけが持ち、外へは渡さない
    /// Mutation surface of the chain connection ledger; held only by the owning component, never handed out
    /// </summary>
    public interface IGearChainConnectionMutation
    {
        void Add(BlockInstanceId partnerId, IGearEnergyTransformer transformer, ConnectionLineRecord record);
        bool TryRemove(BlockInstanceId partnerId, out ConnectionLineRecord record);
        void Clear();
    }
}
