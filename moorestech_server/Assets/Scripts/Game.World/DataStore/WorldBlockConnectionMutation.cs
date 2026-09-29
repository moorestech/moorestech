using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component.WorldMutation;

namespace Game.World.DataStore
{
    internal sealed class WorldBlockConnectionMutation
    {
        private readonly List<IBlockWorldMutation> _mutations = new();

        internal WorldBlockConnectionMutation(IBlock block)
        {
            // 変更対象のコンポーネントだけを通知する
            // Notify only the components of the changed block
            foreach (var participant in block.ComponentManager.GetComponents<IBlockWorldMutationParticipant>())
                _mutations.Add(participant.CaptureWorldMutation());
        }

        internal void ApplyAfterMutation()
        {
            foreach (var mutation in _mutations) mutation.ApplyAfterMutation();
        }
    }
}
