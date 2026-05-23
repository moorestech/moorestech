using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Game.Train.Unit;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityEngine;
using VContainer;

namespace Client.DebugSystem
{
    public class TrainRidingDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Train Riding Debug";

        // 注意: このシートは TrainCarRidingState を直接書き換えるだけで RideActionProtocol を経由しない。
        // 実 parent / 降車 pose は PlayerStateController 経由（UIState遷移後）で次フレーム以降に行われる。
        // Note: this sheet only mutates TrainCarRidingState and bypasses RideActionProtocol.
        // Actual parenting / dismount pose runs on a later frame via PlayerStateController (after the UI state transitions).
        public override IEnumerator Initialize()
        {
            var resolver = ClientDIContext.DIContainer?.DIContainerResolver;
            if (resolver == null)
            {
                AddLabel("DI resolver is not available");
                yield break;
            }

            var trainCarRidingState = resolver.Resolve<TrainCarRidingState>();
            var trainUnitClientCache = resolver.Resolve<TrainUnitClientCache>();

            AddButton("強制降車", clicked: () =>
            {
                if (!trainCarRidingState.IsRiding)
                {
                    Debug.Log("Train riding state is already clear.");
                }

                trainCarRidingState.ClearRidingTrainCar();
            });

            var rows = new List<TrainCarDebugRow>();
            foreach (var pair in trainUnitClientCache.Units)
            {
                var unit = pair.Value;
                var cars = unit.Cars;
                for (var i = 0; i < cars.Count; i++)
                {
                    var carSnapshot = cars[i];
                    rows.Add(new TrainCarDebugRow(
                        carSnapshot.TrainCarInstanceId,
                        $"Car: {i} | Speed: {unit.CurrentSpeed:F2}",
                        unit.TrainUnitInstanceId.ToString(),
                        carSnapshot.TrainCarInstanceId.ToString()));
                }
            }

            rows.Sort((x, y) =>
            {
                var trainCompare = string.CompareOrdinal(x.TrainIdText, y.TrainIdText);
                if (trainCompare != 0)
                {
                    return trainCompare;
                }

                return string.CompareOrdinal(x.CarIdText, y.CarIdText);
            });

            if (rows.Count <= 0)
            {
                AddLabel("No train cars found");
                yield break;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                AddButton(row.Label, clicked: () =>
                {
                    // 既に乗車中なら一旦解除してから上書きする（PlayerStateController 側で再 parent される）。
                    // Clear first when already riding, then overwrite (PlayerStateController re-parents on the new car).
                    trainCarRidingState.ClearRidingTrainCar();
                    trainCarRidingState.SetRidingTrainCar(row.TrainCarInstanceId, 0);
                });
            }

            yield break;
        }

        private readonly struct TrainCarDebugRow
        {
            public readonly TrainCarInstanceId TrainCarInstanceId;
            public readonly string Label;
            public readonly string TrainIdText;
            public readonly string CarIdText;

            public TrainCarDebugRow(TrainCarInstanceId trainCarInstanceId, string label, string trainIdText, string carIdText)
            {
                TrainCarInstanceId = trainCarInstanceId;
                Label = label;
                TrainIdText = trainIdText;
                CarIdText = carIdText;
            }
        }
    }
}
