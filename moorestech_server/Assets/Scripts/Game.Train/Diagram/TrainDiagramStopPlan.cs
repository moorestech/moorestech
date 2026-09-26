using Game.Train.RailGraph;

namespace Game.Train.Diagram
{
    // 時刻表へ積む停車駅1件の指定（停車ノードと出発条件）
    // One requested stop for a timetable: the stop node and its departure condition
    public readonly struct TrainDiagramStopPlan
    {
        public TrainDiagramStopPlan(IRailNode node, TrainDiagram.DepartureConditionType departureConditionType, int waitTicks)
        {
            Node = node;
            DepartureConditionType = departureConditionType;
            WaitTicks = waitTicks;
        }

        public IRailNode Node { get; }
        public TrainDiagram.DepartureConditionType DepartureConditionType { get; }
        public int WaitTicks { get; }
    }
}
