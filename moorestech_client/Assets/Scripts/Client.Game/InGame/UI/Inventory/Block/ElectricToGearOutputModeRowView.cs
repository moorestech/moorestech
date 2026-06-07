using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ElectricToGearOutputModeRowView : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private TMP_Text label;

        public IObservable<int> OnSelectRequested => _onSelectRequested;
        private readonly Subject<int> _onSelectRequested = new Subject<int>();

        private int _index;

        // 行を初期化。index と出力値を表示し、Toggle ON 操作で選択を通知する。
        // Initialize a row: show index and output values; notify selection when toggled on.
        public void Build(int index, double rpm, double torque, double requiredPower, ToggleGroup group)
        {
            _index = index;
            toggle.group = group;
            label.text = $"{index}:  {rpm:0}rpm   {torque:0}trq   {requiredPower:0}W";
            toggle.onValueChanged.AddListener(isOn =>
            {
                // ユーザー操作で ON になった時だけ送信（SetIsOnWithoutNotify では発火しない）。
                // Only emit when turned on by user action (SetIsOnWithoutNotify does not fire this).
                if (isOn) _onSelectRequested.OnNext(_index);
            });
        }

        // 通知を出さずに選択表示だけ更新する（StateDetail 反映用。送信ループを防ぐ）。
        // Update the selected display without firing (for StateDetail sync; prevents a send loop).
        public void SetSelectedWithoutNotify(bool selected)
        {
            toggle.SetIsOnWithoutNotify(selected);
        }

        private void OnDestroy()
        {
            _onSelectRequested.Dispose();
        }
    }
}
