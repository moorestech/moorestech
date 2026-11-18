using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// UI が送る停車駅に固定の出発条件を付ける唯一の口（TrainTimetableCommands が呼ぶ）
    /// The single place attaching the fixed departure condition to UI-sent stops (called by TrainTimetableCommands)
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
