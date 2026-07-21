using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    /// ワールドピンの射影結果を保持しWebへ配信する。毎フレーム呼ばれても変化があった時だけpublishする。
    /// Holds projected world pins for the web; publishes only when a frame actually changes the state.
    /// </summary>
    public class WorldPinStateStore
    {
        public static readonly WorldPinStateStore Instance = new();

        // 正規化座標の同値判定しきい値。これ未満の揺れは配信しない
        // Epsilon for normalized-coordinate equality; jitter below this is not published
        private const float PositionEpsilon = 0.002f;

        private readonly Subject<WorldPinPresentationData> _onChanged = new();
        private readonly List<WorldPinData> _pins = new();
        private int _revision;

        public IObservable<WorldPinPresentationData> ObserveChanged()
        {
            return _onChanged;
        }

        public WorldPinPresentationData GetCurrent()
        {
            return CreateData();
        }

        public void SetPin(string pinId, string text, WorldPinProjection projection)
        {
            var existing = _pins.FirstOrDefault(pin => pin.PinId == pinId);
            if (existing != null && IsSame(existing)) return;

            if (existing == null)
            {
                existing = new WorldPinData { PinId = pinId };
                _pins.Add(existing);
            }

            existing.Text = text;
            existing.ScreenX = projection.ScreenX;
            existing.ScreenY = projection.ScreenY;
            existing.OnScreen = projection.OnScreen;
            existing.DirectionX = projection.DirectionX;
            existing.DirectionY = projection.DirectionY;
            Publish();

            #region Internal

            bool IsSame(WorldPinData pin)
            {
                return pin.Text == text &&
                       pin.OnScreen == projection.OnScreen &&
                       Mathf.Abs(pin.ScreenX - projection.ScreenX) < PositionEpsilon &&
                       Mathf.Abs(pin.ScreenY - projection.ScreenY) < PositionEpsilon &&
                       Mathf.Abs(pin.DirectionX - projection.DirectionX) < PositionEpsilon &&
                       Mathf.Abs(pin.DirectionY - projection.DirectionY) < PositionEpsilon;
            }

            #endregion
        }

        public void RemovePin(string pinId)
        {
            var removed = _pins.RemoveAll(pin => pin.PinId == pinId);
            if (0 < removed) Publish();
        }

        private void Publish()
        {
            _revision++;
            _onChanged.OnNext(CreateData());
        }

        private WorldPinPresentationData CreateData()
        {
            return new WorldPinPresentationData
            {
                Revision = _revision,
                Pins = _pins.Select(pin => new WorldPinData
                {
                    PinId = pin.PinId,
                    Text = pin.Text,
                    ScreenX = pin.ScreenX,
                    ScreenY = pin.ScreenY,
                    OnScreen = pin.OnScreen,
                    DirectionX = pin.DirectionX,
                    DirectionY = pin.DirectionY,
                }).ToArray(),
            };
        }
    }
}
