using System;
using System.Collections.Generic;
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

        public void SetPin(string pinId, string tutorialGuid, WorldPinProjection projection)
        {
            var existing = FindPin(pinId);
            if (existing != null && IsSame(existing)) return;

            if (existing == null)
            {
                existing = new WorldPinData { PinId = pinId };
                _pins.Add(existing);
            }

            existing.TutorialGuid = tutorialGuid;
            existing.ScreenX = projection.ScreenX;
            existing.ScreenY = projection.ScreenY;
            existing.OnScreen = projection.OnScreen;
            existing.DirectionX = projection.DirectionX;
            existing.DirectionY = projection.DirectionY;
            Publish();

            #region Internal

            WorldPinData FindPin(string targetPinId)
            {
                // 毎フレーム呼ばれるのでLINQのクロージャ確保を避ける
                // Called every frame, so avoid the LINQ closure allocation
                foreach (var pin in _pins)
                {
                    if (pin.PinId == targetPinId) return pin;
                }

                return null;
            }

            bool IsSame(WorldPinData pin)
            {
                return pin.TutorialGuid == tutorialGuid &&
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
            // 対象不在でも毎フレーム呼ばれる経路なので、ラムダの確保を避けて走査する
            // This path is hit every frame even with nothing to remove, so scan without allocating a lambda
            for (var i = 0; i < _pins.Count; i++)
            {
                if (_pins[i].PinId != pinId) continue;

                _pins.RemoveAt(i);
                Publish();
                return;
            }
        }

        private void Publish()
        {
            _revision++;
            _onChanged.OnNext(CreateData());
        }

        private WorldPinPresentationData CreateData()
        {
            // 配信1回につき配列1本。Selectのイテレータ確保を避ける
            // One array per publish; avoid the Select iterator allocation
            var pins = new WorldPinData[_pins.Count];
            for (var i = 0; i < _pins.Count; i++)
            {
                var pin = _pins[i];
                pins[i] = new WorldPinData
                {
                    PinId = pin.PinId,
                    TutorialGuid = pin.TutorialGuid,
                    ScreenX = pin.ScreenX,
                    ScreenY = pin.ScreenY,
                    OnScreen = pin.OnScreen,
                    DirectionX = pin.DirectionX,
                    DirectionY = pin.DirectionY,
                };
            }

            return new WorldPinPresentationData { Revision = _revision, Pins = pins };
        }
    }
}
