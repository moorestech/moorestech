using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    // 撤去の返却計算が読む「現在のワールドの構成」を、構成が変わるまで使い回す。一括撤去で搬出元1個ごとにワールド全体を組み直さないため
    // Reuses the current world layout read by removal refunds until the layout changes, so a bulk removal does not rebuild the whole world per source block
    internal sealed class BeltRemovalWorldLayoutCache
    {
        private readonly IWorldBlockDatastore _world;
        private BeltSegmentLayout[] _layouts;

        internal BeltRemovalWorldLayoutCache(IWorldBlockDatastore world)
        {
            _world = world;
        }

        // 構成に関わるblockの設置・撤去で呼ぶ
        // Called when a block that affects the layout is placed or removed
        internal void Invalidate()
        {
            _layouts = null;
        }

        internal HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)> GetRemovedInputs(IBlock block)
        {
            if (!BeltConnectionRemovalRefund.IsSourceConnectedToBelt(block)) return new HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)>();

            // 同tickの先行撤去を反映した現在の接続を読む。組はtick末尾まで作り直さないので組の接続は古い
            // Read current connections including earlier removals in the same tick; the assembly is not rebuilt until the tick end, so its connections are stale
            _layouts ??= BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(_world));
            return BeltConnectionRemovalRefund.GetRemovedInputs(block, _layouts);
        }
    }
}
