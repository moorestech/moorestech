using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Orbit;
using UniRx;

namespace Tests.Util.Rocket
{
    // 巨大数量の在庫境界試験にだけ使う固定成果
    // Fixed outputs used only for large-count inventory boundary tests
    internal sealed class FixedOutputArrivalService : IOrbitalArrivalService
    {
        private readonly IReadOnlyList<IItemStack> _outputs;
        internal FixedOutputArrivalService(IReadOnlyList<IItemStack> outputs) => _outputs = outputs;
        public IObservable<OrbitalArrival> OnOrbitalArrival => Observable.Empty<OrbitalArrival>();
        public bool IsOrbitalPayload(ItemId itemId) => true;
        public IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId itemId, int count) => _outputs;
        public IReadOnlyList<IItemStack> Arrive(ItemId itemId, int count) => _outputs;
    }
}
