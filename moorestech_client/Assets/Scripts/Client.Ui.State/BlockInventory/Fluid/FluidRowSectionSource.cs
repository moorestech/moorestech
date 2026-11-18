using System;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using UniRx;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行区画の値の流れ。どちらの通知でも判断し直す
    /// Fluid-row values; redecide on either update
    /// </summary>
    public sealed class FluidRowSectionSource : IObservable<SectionContent<BlockFluidSlotsViewModel>>
    {
        private readonly IBlockFluidSlotsModel _fluids;
        private readonly IMachineSectionModel _machine;

        public FluidRowSectionSource(IBlockFluidSlotsModel fluids, IMachineSectionModel machine)
        {
            _fluids = fluids;
            _machine = machine;
        }

        public IDisposable Subscribe(IObserver<SectionContent<BlockFluidSlotsViewModel>> observer)
        {
            // 2 本を別購読し今の 2 値で組み直す
            // Subscribe separately; rebuild from both values
            var subscriptions = new CompositeDisposable();
            _fluids.Current.Subscribe(_ => observer.OnNext(Content())).AddTo(subscriptions);
            _machine.Current.Subscribe(_ => observer.OnNext(Content())).AddTo(subscriptions);
            return subscriptions;

            #region Internal

            SectionContent<BlockFluidSlotsViewModel> Content()
            {
                var fluids = _fluids.Current.Value;
                return FluidRowVisibility.Shows(fluids, _machine.Current.Value.IsPresent) ? SectionContent<BlockFluidSlotsViewModel>.Present(fluids) : SectionContent<BlockFluidSlotsViewModel>.Absent;
            }

            #endregion
        }
    }
}
