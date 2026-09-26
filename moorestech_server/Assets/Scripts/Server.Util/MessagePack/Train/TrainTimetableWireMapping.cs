using Game.Train.Diagram;
using Game.Train.RailGraph;

namespace Server.Util.MessagePack
{
    // 時刻表のワイヤ値とドメイン値の写像を1箇所へ閉じる
    // Keep the mapping between timetable wire values and domain values in one place
    public static class TrainTimetableWireMapping
    {
        public static TrainTimetableStopSideWireValue ToWire(StationNodeSide side)
        {
            return side == StationNodeSide.Front
                ? TrainTimetableStopSideWireValue.Front
                : TrainTimetableStopSideWireValue.Back;
        }

        public static bool TryToStationNodeSide(TrainTimetableStopSideWireValue wireValue, out StationNodeSide side)
        {
            switch (wireValue)
            {
                case TrainTimetableStopSideWireValue.Front:
                    side = StationNodeSide.Front;
                    return true;
                case TrainTimetableStopSideWireValue.Back:
                    side = StationNodeSide.Back;
                    return true;
                default:
                    // Unspecifiedと未定義値はここで止める。呼び出し側が理由付きで拒否する
                    // Unspecified and undefined values stop here; the caller rejects them with a reason
                    side = StationNodeSide.Front;
                    return false;
            }
        }

        public static TrainTimetableDepartureConditionWireValue ToWire(TrainDiagram.DepartureConditionType departureConditionType)
        {
            switch (departureConditionType)
            {
                case TrainDiagram.DepartureConditionType.TrainInventoryFull:
                    return TrainTimetableDepartureConditionWireValue.TrainInventoryFull;
                case TrainDiagram.DepartureConditionType.TrainInventoryEmpty:
                    return TrainTimetableDepartureConditionWireValue.TrainInventoryEmpty;
                default:
                    return TrainTimetableDepartureConditionWireValue.WaitForTicks;
            }
        }

        public static bool TryToDepartureConditionType(
            TrainTimetableDepartureConditionWireValue wireValue, out TrainDiagram.DepartureConditionType departureConditionType)
        {
            switch (wireValue)
            {
                case TrainTimetableDepartureConditionWireValue.TrainInventoryFull:
                    departureConditionType = TrainDiagram.DepartureConditionType.TrainInventoryFull;
                    return true;
                case TrainTimetableDepartureConditionWireValue.TrainInventoryEmpty:
                    departureConditionType = TrainDiagram.DepartureConditionType.TrainInventoryEmpty;
                    return true;
                case TrainTimetableDepartureConditionWireValue.WaitForTicks:
                    departureConditionType = TrainDiagram.DepartureConditionType.WaitForTicks;
                    return true;
                default:
                    departureConditionType = TrainDiagram.DepartureConditionType.WaitForTicks;
                    return false;
            }
        }
    }
}
