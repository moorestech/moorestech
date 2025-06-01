using Game.Train.RailGraph;
using System;
using System.Collections.Generic;
namespace Game.Train.Utility
{
    public static class RailNodeCalculate
    {
        public static int CalculateTotalDistance(List<RailNode> _railNodes)
        {
            int totalDistance = 0;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                totalDistance += _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
            }
            //万が一int maxを超える場合エラー
            if (totalDistance < 0)
            {
                throw new InvalidOperationException("列車の長さまたは計算経路がInt.Maxを超えています。");
            }
            return totalDistance;
        }
    }
}
