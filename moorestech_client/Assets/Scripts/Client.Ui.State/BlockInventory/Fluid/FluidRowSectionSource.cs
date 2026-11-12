using System;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using UniRx;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行の区画の値の流れ。液体と機械のどちらの通知でも FluidRowVisibility で同じ判断をやり直す（購読の寿命は購読した側が持つ）
    /// Value stream of the fluid-row section; redoes the same FluidRowVisibility decision on either fluid or machine updates (the subscriber owns the lifetime)
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
            // 2 本を別々に購読し、どちらの通知でも今の 2 値で組み直す
            // Subscribe to both separately and rebuild from the current two values on either
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
