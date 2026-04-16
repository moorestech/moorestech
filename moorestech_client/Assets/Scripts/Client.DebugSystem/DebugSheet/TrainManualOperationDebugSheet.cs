using System.Collections;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Train.Unit;
using UnityDebugSheet.Runtime.Core.Scripts;
using VContainer;

namespace Client.DebugSystem
{
    public class TrainManualOperationDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Train Manual Operation";

        public override IEnumerator Initialize()
        {
            var trainManualOperationState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<TrainManualOperationState>();
            var trainUnitClientCache = ClientDIContext.DIContainer.DIContainerResolver.Resolve<TrainUnitClientCache>();
            var sortedTrains = trainUnitClientCache.Units
                .OrderBy(pair => pair.Key.AsPrimitive())
                .ToList();

            AddButton("Stop Manual Operation", subText: "Stop current operation", clicked: () =>
            {
                trainManualOperationState.StopOperating();
                DebugSheetController.CloseDebugSheet();
            });

            for (var i = 0; i < sortedTrains.Count; i++)
            {
                var train = sortedTrains[i];
                var label = $"Train Unit {i}";
                var speedText = $"Speed {train.Value.CurrentSpeed:F1}";
                var carsText = $"Cars {train.Value.Cars.Count}";
                var subText = $"Start UnitId {train.Key.AsPrimitive()}";
                AddButton(label, subText: $"{speedText} {carsText} {subText}", clicked: () =>
                {
                    trainManualOperationState.StartOperating(train.Key);
                    DebugSheetController.CloseDebugSheet();
                });
            }

            yield break;
        }
    }
}
