using System;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     設置高さオフセットの唯一の正。通常設置・ベルトの実オフセットとHUD表示は全てここを読む
    ///     The single source of truth for the placement height offset; placement systems and the HUD all read it
    /// </summary>
    public class PlacementHeightOffset
    {
        private readonly ReactiveProperty<int> _value = new(0);

        public int Value => _value.Value;
        public IObservable<int> OnChanged => _value;

        internal void SetValue(int value)
        {
            _value.Value = value;
        }
    }
}
