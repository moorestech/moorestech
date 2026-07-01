using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public static class GearNetworkRotationCacheBuilder
    {
        private const float RatioTolerance = 0.0001f;

        public static bool Rebuild(Dictionary<BlockInstanceId, GearRotationInfo> rotationInfos, IGearGenerator originGenerator)
        {
            rotationInfos.Clear();
            var originInfo = new GearRotationInfo(originGenerator, 1f, originGenerator.GenerateIsClockwise);
            rotationInfos.Add(originGenerator.BlockInstanceId, originInfo);

            // topology変更時だけ接続を辿り、RPM比と回転方向をcacheする。
            // Walk connections only on topology changes and cache RPM ratios plus direction.
            var queue = new Queue<GearRotationInfo>();
            queue.Enqueue(originInfo);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var connect in current.EnergyTransformer.GetGearConnects())
                {
                    if (!TryAdd(rotationInfos, originGenerator, current, connect, out var nextInfo)) return false;
                    if (nextInfo != null) queue.Enqueue(nextInfo);
                }
            }
            return true;
        }

        private static bool TryAdd(Dictionary<BlockInstanceId, GearRotationInfo> rotationInfos, IGearGenerator originGenerator, GearRotationInfo current, GearConnect connect, out GearRotationInfo nextInfo)
        {
            nextInfo = null;
            var nextClockwise = connect.Self.IsReverse && connect.Target.IsReverse ? !current.IsClockwise : current.IsClockwise;
            var nextRatio = CalculateNextRatio(current, connect);
            if (rotationInfos.TryGetValue(connect.Transformer.BlockInstanceId, out var known))
            {
                return known.IsClockwise == nextClockwise && Math.Abs(known.RpmRatio - nextRatio) <= RatioTolerance;
            }

            if (connect.Transformer is IGearGenerator generator &&
                generator.BlockInstanceId != originGenerator.BlockInstanceId &&
                generator.GenerateIsClockwise != nextClockwise)
            {
                return false;
            }

            nextInfo = new GearRotationInfo(connect.Transformer, nextRatio, nextClockwise);
            rotationInfos.Add(connect.Transformer.BlockInstanceId, nextInfo);
            return true;
        }

        private static float CalculateNextRatio(GearRotationInfo current, GearConnect connect)
        {
            if (connect.Transformer is IGear gear &&
                current.EnergyTransformer is IGear currentGear &&
                connect.Self.IsReverse &&
                connect.Target.IsReverse)
            {
                return current.RpmRatio * currentGear.TeethCount / gear.TeethCount;
            }
            return current.RpmRatio;
        }
    }
}
