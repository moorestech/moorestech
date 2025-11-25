using Game.Train.RailGraph;
using System;
using System.Collections.Generic;
namespace Game.Train.Utility
{
    public static class RailNodeCalculate
    {
        //配列を後ろから見ていく
        public static int CalculateTotalDistance(List<RailNode> _railNodes)
        {
            int totalDistance = 0;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                var leng = _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
                if (leng == -1) throw new InvalidOperationException("経路が繋がっていません");
                totalDistance += leng;
            }
            //万が一int maxを超える場合エラー
            if (totalDistance < 0)
            {
                throw new InvalidOperationException("列車の長さまたは計算経路がInt.Maxを超えています。");
            }
            return totalDistance;
        }

        //配列を前から見ていく
        public static int CalculateTotalDistanceF(List<RailNode> _railNodes)
        {
            int totalDistance = 0;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                var leng = _railNodes[i].GetDistanceToNode(_railNodes[i + 1]);
                if (leng == -1) throw new InvalidOperationException("経路が繋がっていません");
                totalDistance += leng;
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
