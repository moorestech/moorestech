using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using UniRx;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細区画の値の流れ。液体が 1 件以上で在る
    /// Fluid-details values; present with 1+ fluids
    /// </summary>
    public sealed class FluidDetailsSectionSource : IObservable<SectionContent<IReadOnlyList<BlockFluidSlotState>>>
    {
        private readonly IBlockFluidSlotsModel _fluids;

        public FluidDetailsSectionSource(IBlockFluidSlotsModel fluids)
        {
            _fluids = fluids;
        }

        public IDisposable Subscribe(IObserver<SectionContent<IReadOnlyList<BlockFluidSlotState>>> observer)
        {
            return _fluids.Current.Subscribe(fluids => observer.OnNext(0 < fluids.Slots.Count ? SectionContent<IReadOnlyList<BlockFluidSlotState>>.Present(fluids.Slots) : SectionContent<IReadOnlyList<BlockFluidSlotState>>.Absent));
        }
    }
}
