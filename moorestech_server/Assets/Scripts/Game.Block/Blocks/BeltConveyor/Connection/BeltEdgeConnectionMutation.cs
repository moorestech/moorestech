using System.Collections.Generic;
using Game.Block.Interface.Component.WorldMutation;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal sealed class BeltEdgeConnectionMutation : IBlockWorldMutation
    {
        private readonly BeltInventoryConnectionContext _context;
        private readonly List<BeltEdgeConnection> _before;

        internal BeltEdgeConnectionMutation(BeltInventoryConnectionContext context, List<BeltEdgeConnection> before)
        {
            _context = context;
            _before = before;
        }

        public void ApplyAfterMutation()
        {
            var after = _context.GetOverride();
            // 第三者sourceも含め、旧接続を全て外してから新接続を張る
            // Remove all old connections before adding new ones, including third-party sources
            foreach (var connection in _before)
                if (!after.Contains(connection)) connection.Source.RemoveConnection(connection.Target);
            foreach (var connection in after)
                if (!_before.Contains(connection)) connection.Source.SetConnection(connection.Target, connection.Info);
        }
    }
}
