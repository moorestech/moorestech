using Game.Train.RailGraph;
using System;
using System.Collections.Generic;
namespace Game.Train.RailCalc
{
    public static class RailNodeCalculate
    {
        //配列を後ろから見ていく
        public static int CalculateTotalDistance(IReadOnlyList<IRailNode> _railNodes)
        {
            int totalDistance = 0;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                var leng = _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
                if (leng == -1) throw new InvalidOperationException("Route is not connected");
                totalDistance += leng;
            }
            //万が一int maxを超える場合エラー
            if (totalDistance < 0)
            {
                throw new InvalidOperationException("Train length or calculated route exceeds Int.Max.");
            }
            return totalDistance;
        }

        //配列を前から見ていく
        public static int CalculateTotalDistanceF(IReadOnlyList<IRailNode> _railNodes)
        {
            int totalDistance = 0;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                var leng = _railNodes[i].GetDistanceToNode(_railNodes[i + 1]);
                if (leng == -1) throw new InvalidOperationException("Route is not connected");
                totalDistance += leng;
            }
            //万が一int maxを超える場合エラー
            if (totalDistance < 0)
            {
                throw new InvalidOperationException("Train length or calculated route exceeds Int.Max.");
            }
            return totalDistance;
        }

    }
}
