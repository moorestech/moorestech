using System;
using System.Collections.Generic;
using Core.BeltTransport;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // 網生成で使う乱択をシード付きの1つの乱数列へまとめる
    // Collects every random choice of network generation on one seeded sequence
    public sealed class RandomBeltChoices
    {
        private static readonly BeltDirection[] AllDirections = { BeltDirection.Front, BeltDirection.Back, BeltDirection.Left, BeltDirection.Right };
        private readonly Random _random;

        public RandomBeltChoices(int seed)
        {
            _random = new Random(seed);
        }

        public int NextSeed()
        {
            return _random.Next();
        }

        public int Next(int maxExclusive)
        {
            return _random.Next(maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        // 1割で速度0、それ以外は1～MaxSpeed
        // Speed 0 one time in ten, otherwise 1..MaxSpeed
        public int Speed()
        {
            return _random.Next(10) == 0 ? 0 : _random.Next(1, BeltConstants.MaxSpeed + 1);
        }

        public BeltDirection Direction()
        {
            return AllDirections[_random.Next(4)];
        }

        // straightを先頭にした3方向を無作為に並べ替える
        // Shuffle the three directions that start with straight
        public BeltDirection[] ShuffledCandidates(BeltDirection straight)
        {
            var order = BeltPriority.Create(straight);
            var result = new BeltDirection[3];
            for (var i = 0; i < 3; i++) result[i] = (BeltDirection)BeltPriority.Direction(order, i);
            Shuffle(result);
            return result;
        }

        // 方向マスクで使用済みでない方向から1つ選ぶ
        // Pick one direction not yet used in the direction mask
        public BeltDirection PickUnused(int usedMask)
        {
            var free = new List<BeltDirection>();
            foreach (var direction in AllDirections)
                if ((usedMask & (1 << (int)direction)) == 0) free.Add(direction);
            return free[_random.Next(free.Count)];
        }

        public void Shuffle<T>(IList<T> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
