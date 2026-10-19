using System;
using System.Collections.Generic;

namespace Tests.Util.Throughput
{
    // ブロックの設置順。設置順の偶然で通るのを防ぐため、全シナリオを3通りで回す（ADR 0084）
    // Block placement order; every scenario runs all three so a lucky placement order cannot make it pass (ADR 0084)
    public enum BeltPlacementOrder
    {
        Forward,
        Reverse,
        RandomSeed1,
    }

    public static class BeltPlacementOrderExtension
    {
        // 決定論の並べ替え。ランダムは固定シード1本の Fisher–Yates
        // Deterministic reordering; random is a Fisher–Yates shuffle with one fixed seed
        public static List<T> Apply<T>(this BeltPlacementOrder order, IReadOnlyList<T> source)
        {
            var result = new List<T>(source);
            switch (order)
            {
                case BeltPlacementOrder.Reverse:
                    result.Reverse();
                    break;
                case BeltPlacementOrder.RandomSeed1:
                    var random = new Random(1);
                    for (var i = result.Count - 1; 0 < i; i--)
                    {
                        var j = random.Next(i + 1);
                        (result[i], result[j]) = (result[j], result[i]);
                    }
                    break;
            }
            return result;
        }
    }
}
