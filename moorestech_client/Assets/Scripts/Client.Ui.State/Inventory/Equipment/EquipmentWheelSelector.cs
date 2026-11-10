using System.Collections.Generic;
using Client.Ui.State.Inventory.Logic;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// ホイールで装備を循環する唯一の場所（Web EquipmentPanel の写し）
    /// The single place that cycles equipment with the wheel (Web EquipmentPanel)
    /// </summary>
    public sealed class EquipmentWheelSelector : IInitializable
    {
        private readonly IEquipmentSelectionPort _port;
        private readonly List<int> _pending = new();
        private double _remainder;
        private int _lastRevision;

        public EquipmentWheelSelector(IEquipmentSelectionPort port)
        {
            _port = port;
        }

        public void Initialize()
        {
            _lastRevision = _port.ConfirmationRevision;
            _port.OnSelectionChanged.Subscribe(_ => TrimConfirmed());

            #region Internal

            void TrimConfirmed()
            {
                // 世代が進んだ数だけ頭から確定済みとして捨てる（世代は単調増加。Web の「戻ったら全部捨てる」は再接続用で写さない）
                // Drop as many head entries as the revision advanced (it only increases; Web's reset-on-rewind is for reconnects and is not copied)
                var revision = _port.ConfirmationRevision;
                var advanced = revision - _lastRevision;
                _lastRevision = revision;
                _pending.RemoveRange(0, System.Math.Min(advanced, _pending.Count));
            }

            #endregion
        }

        public void Wheel(double delta, WheelDeltaUnit unit)
        {
            // 1 枠以下は切り替え先が無いので、累積もしない
            // With one slot or fewer there is nowhere to switch, so nothing accumulates either
            var slotCount = _port.SlotCount;
            // silent-ok: 枠 1 以下は切り替え先が無く Web も送らない / EquipmentWheelSelectorTest「装備枠が1つならホイールで選択しない」が見る
            if (slotCount <= 1) return;

            var accumulated = EquipmentSelectionLogic.AccumulateWheelSteps(_remainder, delta, unit);
            _remainder = accumulated.Remainder;
            // silent-ok: 1 段に満たない量は端数として _remainder に残し次のノッチで足す（Web も送らない） / EquipmentWheelFlowTest「小さいホイールは累積して閾値を越えたときだけ切り替える」が見る
            if (accumulated.Steps == 0) return;

            // 未確認の末尾を起点にし、古いエコーへ巻き戻されないようにする
            // Start from the unconfirmed tail so a stale echo never rewinds the next notch
            var current = _pending.Count == 0 ? _port.SelectedIndex : _pending[_pending.Count - 1];
            var index = EquipmentSelectionLogic.CycleEquipment(current, accumulated.Steps, slotCount);
            _pending.Add(index);
            _port.Select(index);
        }
    }
}
