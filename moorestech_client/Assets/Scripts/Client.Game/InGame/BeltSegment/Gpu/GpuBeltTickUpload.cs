using Game.BeltSegment;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltTickUpload
    {
        internal readonly GpuBeltEvent[] Events;
        readonly bool[] seenInputs, seenOutputs, seenSpeeds;
        readonly int[] speedIds, speedValues;

        internal GpuBeltTickUpload(int inputCount, int outputCount, int segmentCount)
        {
            Events = new GpuBeltEvent[2 * inputCount + outputCount + segmentCount];
            seenInputs = new bool[inputCount];
            seenOutputs = new bool[outputCount];
            seenSpeeds = new bool[segmentCount];
            speedIds = new int[segmentCount];
            speedValues = new int[segmentCount];
        }

        internal int Prepare(BeltReplayTick tick)
        {
            System.Array.Clear(seenInputs, 0, seenInputs.Length);
            System.Array.Clear(seenOutputs, 0, seenOutputs.Length);
            System.Array.Clear(seenSpeeds, 0, seenSpeeds.Length);
            int count = 0;
            // 正集合を冪等化し、同じ速度IDの最後の値を残す。
            // Deduplicate positive sets and retain the last speed for each ID.
            foreach (int id in tick.ReadyInputs)
                if (!seenInputs[id])
                {
                    seenInputs[id] = true;
                    Events[count++] = new GpuBeltEvent { Kind = GpuBeltData.ReadyInput, Id = id, Value = 1 };
                }
            foreach (int id in tick.SuccessfulOutputs)
                if (!seenOutputs[id])
                {
                    seenOutputs[id] = true;
                    Events[count++] = new GpuBeltEvent { Kind = GpuBeltData.SuccessfulOutput, Id = id, Value = 1 };
                }
            int speedCount = 0;
            foreach (var change in tick.SpeedChanges)
            {
                if (!seenSpeeds[change.SegmentId])
                {
                    seenSpeeds[change.SegmentId] = true;
                    speedIds[speedCount++] = change.SegmentId;
                }
                speedValues[change.SegmentId] = change.Speed;
            }
            for (int i = 0; i < speedCount; i++)
            {
                int id = speedIds[i];
                Events[count++] = new GpuBeltEvent { Kind = GpuBeltData.Speed, Id = id, Value = speedValues[id] };
            }
            foreach (var insertion in tick.Insertions)
                Events[count++] = new GpuBeltEvent
                {
                    Kind = GpuBeltData.Insertion, Id = insertion.InputId,
                    Value = insertion.Length, Extra = insertion.Item.ItemId
                };
            return count;
        }
    }
}
