using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using System.Linq;
using UniRx;

namespace Game.Orbit
{
    // 積荷表から打ち上げ成果を決め、軌道到着を流す
    // Decides arrival outputs from the payload table and publishes orbital arrivals
    public class OrbitalArrivalService : IOrbitalArrivalService
    {
        public IObservable<OrbitalArrival> OnOrbitalArrival => _onOrbitalArrival;

        private readonly Subject<OrbitalArrival> _onOrbitalArrival = new();
        private readonly Dictionary<ItemId, (int CountPerLaunch, (ItemId ItemId, int Count)[] ArrivalOutputs)> _payloadByItemId = new();

        public OrbitalArrivalService()
        {
            foreach (var payload in MasterHolder.ItemMaster.Items.OrbitalPayloads)
                _payloadByItemId.Add(MasterHolder.ItemMaster.GetItemId(payload.PayloadItemGuid), (payload.CountPerLaunch, payload.ArrivalOutputs.Select(output => (MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count)).ToArray()));
        }

        public bool IsOrbitalPayload(ItemId itemId)
        {
            return _payloadByItemId.ContainsKey(itemId);
        }

        public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount)
        {
            var payload = FindPayload();

            // 全成果を整数範囲で検証し、部分的な成果生成を防ぐ
            // Validate every output count before creating any output stacks
            var launchCount = payloadCount / payload.CountPerLaunch;
            var outputCounts = new List<int>();
            foreach (var output in payload.ArrivalOutputs)
            {
                var totalCount = (long)output.Count * launchCount;
                if (int.MaxValue < totalCount)
                    throw new ArgumentOutOfRangeException(nameof(payloadCount), payloadCount, $"打ち上げ成果の個数が整数範囲を超えます ItemId:{payloadItemId} OutputItemGuid:{output.ItemId} TotalCount:{totalCount} MaxCount:{int.MaxValue}");
                outputCounts.Add((int)totalCount);
            }

            // スタック上限を超える成果を分割する
            // Split validated outputs at the stack limit
            var outputs = new List<IItemStack>();
            var outputIndex = 0;
            foreach (var output in payload.ArrivalOutputs)
            {
                var outputItemId = output.ItemId;
                outputs.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(outputItemId, outputCounts[outputIndex]));
                outputIndex++;
            }
            return outputs;

            #region Internal

            (int CountPerLaunch, (ItemId ItemId, int Count)[] ArrivalOutputs) FindPayload()
            {
                if (!_payloadByItemId.TryGetValue(payloadItemId, out var found))
                    throw new InvalidOperationException($"積荷表に無いアイテムの打ち上げ成果を求められました ItemId:{payloadItemId}");
                if (payloadCount <= 0 || payloadCount % found.CountPerLaunch != 0)
                    throw new ArgumentException($"積荷の個数が1便の個数の正の整数倍ではありません ItemId:{payloadItemId} Count:{payloadCount} CountPerLaunch:{found.CountPerLaunch}");
                return found;
            }

            #endregion
        }

        public IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount)
        {
            // 先に知らせた成果と同じものを返し、到着を流す
            // Return the same outputs as the preview and publish the arrival
            var outputs = PreviewArrivalOutputs(payloadItemId, payloadCount);
            _onOrbitalArrival.OnNext(new OrbitalArrival(payloadItemId, payloadCount));
            return outputs;
        }
    }
}
