using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// UI が送る停車駅に固定の出発条件を付ける（Web ホスト TrainTimetableStopParser と同じ規則。D-2）
    /// Attaches the fixed departure condition to stops sent from the UI (same rule as the Web host TrainTimetableStopParser; D-2)
    /// </summary>
    public static class TrainTimetableDeparture
    {
        private const TrainDiagram.DepartureConditionType UiFixedDepartureCondition = TrainDiagram.DepartureConditionType.WaitForTicks;

        public static List<TrainTimetableStop> ToServerStops(IReadOnlyList<TimetableReplaceStop> stops)
        {
            var serverStops = new List<TrainTimetableStop>(stops.Count);
            foreach (var stop in stops) serverStops.Add(new TrainTimetableStop(stop.Position, stop.Side, UiFixedDepartureCondition, GameUpdater.TicksPerSecond));
            return serverStops;
        }
    }
}
