using System;
using Game.Gear.Common;
using UniRx;

namespace Game.Block.Blocks.GearToElectric
{
    /// <summary>
    /// 電力tickで供給電力と発電状態を確定し、変化時だけ通知する
    /// Settles power and state each electric tick and notifies only on change
    /// </summary>
    public class GearToElectricGenerationTracker
    {
        public IObservable<Unit> OnGenerationChanged => _onGenerationChanged;
        public float SuppliedPower { get; private set; }
        public GearToElectricGenerationState State { get; private set; } = GearToElectricGenerationState.Stopped;

        private readonly Subject<Unit> _onGenerationChanged = new();
        private readonly float _minimumRpm;
        private bool _hasNotifiedOnce;

        public GearToElectricGenerationTracker(float minimumRpm)
        {
            _minimumRpm = minimumRpm;
        }

        // 放電の直後に現在RPMと供給電力を記録する
        // Record current RPM and supplied power immediately after discharge
        public void RecordElectricTick(RPM currentRpm, float suppliedPower)
        {
            var state = ResolveState();
            // 表示の丸め境界を跨ぐ微小差も直近tickの実供給として届ける
            // Deliver even tiny changes across a display rounding boundary as the latest tick's actual supply
            if (_hasNotifiedOnce && state == State && SuppliedPower == suppliedPower) return;
            _hasNotifiedOnce = true;
            State = state;
            SuppliedPower = suppliedPower;
            _onGenerationChanged.OnNext(Unit.Default);

            #region Internal

            // RPM不足を優先し、回転中だけ供給量で状態を分ける
            // RPM shortage takes priority; supply distinguishes states only while spinning
            GearToElectricGenerationState ResolveState()
            {
                var rpm = currentRpm.AsPrimitive();
                if (rpm <= 0f || rpm < _minimumRpm) return GearToElectricGenerationState.Stopped;
                return 0f < suppliedPower ? GearToElectricGenerationState.Generating : GearToElectricGenerationState.Idle;
            }

            #endregion
        }

    }
}
