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

        public override IEnumerator Initialize()
        {
            var resolver = ClientDIContext.DIContainer?.DIContainerResolver;
            if (resolver == null)
            {
                AddLabel("DI resolver is not available");
                yield break;
            }

            var ridingPlayerController = resolver.Resolve<TrainCarRidingPlayerController>();
            var trainCarRidingState = resolver.Resolve<TrainCarRidingState>();
            var trainUnitClientCache = resolver.Resolve<TrainUnitClientCache>();

            AddButton("強制降車", clicked: () =>
            {
                if (!trainCarRidingState.IsRiding)
                {
                    Debug.Log("Train riding state is already clear.");
                }

                ridingPlayerController.ForceDismount();
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
                        unit.TrainInstanceId.ToString(),
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
                    if (!ridingPlayerController.ForceRide(row.TrainCarInstanceId))
                    {
                        Debug.LogWarning($"Failed to force ride train car: {row.TrainCarInstanceId}");
                    }
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
