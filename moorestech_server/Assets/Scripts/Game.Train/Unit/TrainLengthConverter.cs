using System;
using Game.Train.RailCalc;

namespace Game.Train.Unit
{
    // マスター記載の長さを内部単位へ変換
    // Converts master-defined lengths into current rail units.
    public static class TrainLengthConverter
    {
        // 現在のベジエ尺度へ正規化
        // Normalizes master lengths to the active Bezier scale.
        public static int ToRailUnits(int masterLength)
        {
            var ratio = BezierUtility.RAIL_LENGTH_SCALE;
            return (int)Math.Round(masterLength * ratio);
        }
    }
}
