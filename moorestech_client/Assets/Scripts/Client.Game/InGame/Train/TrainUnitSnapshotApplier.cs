using Client.Network.API;
using Game.Train.Train;
using System.Collections.Generic;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     列車スナップショットを初期化時にキャッシュへ流し込むアプライヤー
    ///     Applies the initial train snapshots to the local cache and can be reused for resync
    /// </summary>
    public sealed class TrainUnitSnapshotApplier : IInitializable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;

        public TrainUnitSnapshotApplier(TrainUnitClientCache cache, InitialHandshakeResponse initialHandshakeResponse)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
        }

        public void Initialize()
        {
            ApplySnapshot(_initialHandshakeResponse?.TrainUnitSnapshots);
        }

        // レスポンスに含まれる列車データをキャッシュへ適用
        // Apply the received snapshot response to the cache
        public void ApplySnapshot(TrainUnitSnapshotResponse response)
        {
            if (response == null)
            {
                return;
            }

            var snapshotPacks = response.Snapshots;
            var bundles = new List<TrainUnitSnapshotBundle>(snapshotPacks?.Count ?? 0);
            if (snapshotPacks != null)
            {
                for (var i = 0; i < snapshotPacks.Count; i++)
                {
                    var pack = snapshotPacks[i];
                    if (pack == null)
                    {
                        continue;
                    }
                    bundles.Add(pack.ToModel());
                }
            }

            _cache.OverrideAll(bundles, response.ServerTick);
        }
    }
}
