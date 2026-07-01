using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public static class GearNetworkComponentFinder
    {
        public static List<List<IGearEnergyTransformer>> FindComponents(GearNetwork network)
        {
            var totalCount = network.GearTransformers.Count + network.GearGenerators.Count;
            var remaining = new IGearEnergyTransformer[totalCount];
            var idToIndex = new Dictionary<BlockInstanceId, int>(totalCount);
            FillRemaining(network, remaining, idToIndex);
            return FindComponents(remaining, idToIndex);
        }

        private static void FillRemaining(GearNetwork network, IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIndex)
        {
            var index = 0;
            foreach (var transformer in network.GearTransformers) Add(transformer);
            foreach (var generator in network.GearGenerators) Add(generator);

            #region Internal

            void Add(IGearEnergyTransformer gear)
            {
                remaining[index] = gear;
                idToIndex[gear.BlockInstanceId] = index;
                index++;
            }

            #endregion
        }

        private static List<List<IGearEnergyTransformer>> FindComponents(IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIndex)
        {
            var components = new List<List<IGearEnergyTransformer>>();
            var queue = new Queue<IGearEnergyTransformer>();
            for (var i = 0; i < remaining.Length; i++)
            {
                var start = remaining[i];
                if (start == null) continue;
                remaining[i] = null;
                var component = new List<IGearEnergyTransformer>();
                queue.Clear();
                queue.Enqueue(start);
                CollectComponent(component, queue, remaining, idToIndex);
                components.Add(component);
            }
            return components;
        }

        private static void CollectComponent(List<IGearEnergyTransformer> component, Queue<IGearEnergyTransformer> queue, IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIndex)
        {
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var connect in current.GetGearConnects())
                {
                    if (!idToIndex.TryGetValue(connect.Transformer.BlockInstanceId, out var index)) continue;
                    if (remaining[index] == null) continue;
                    remaining[index] = null;
                    queue.Enqueue(connect.Transformer);
                }
            }
        }
    }
}
