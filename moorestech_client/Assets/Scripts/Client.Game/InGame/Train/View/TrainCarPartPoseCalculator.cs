using System;
using System.Collections.Generic;

namespace Client.Game.InGame.Train.View
{
    public readonly struct TrainCarPoseResult
    {
        public readonly UnityEngine.Vector3 Position;
        public readonly UnityEngine.Quaternion Rotation;

        public TrainCarPoseResult(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public readonly struct TrainCarPartSpan
    {
        public readonly int FrontOffset;
        public readonly int RearOffset;

        public TrainCarPartSpan(int frontOffset, int rearOffset)
        {
            FrontOffset = frontOffset;
            RearOffset = rearOffset;
        }
    }

    public static class TrainCarPartPoseCalculator
    {
        // part 表示長の比率を、現在の車両 rail 長へ正規化する
        // Normalize visual-part authored lengths to the current car rail length
        public static bool TryBuildNormalizedPartLengths(int carLength, IReadOnlyList<float> authoredPartLengths, int[] normalizedPartLengths, out int partCount)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            partCount = 0;
            if (carLength <= 0 || authoredPartLengths == null || normalizedPartLengths == null)
            {
                return false;
            }
            if (authoredPartLengths.Count == 0 || normalizedPartLengths.Length < authoredPartLengths.Count)
            {
                return false;
            }

            // authored 長の合計を検証し、正規化の基準にする
            // Validate authored total length and use it as the normalization base
            var authoredTotal = 0f;
            for (var i = 0; i < authoredPartLengths.Count; i++)
            {
                var authoredLength = authoredPartLengths[i];
                if (authoredLength <= 0)
                {
                    return false;
                }
                authoredTotal += authoredLength;
            }
            if (authoredTotal <= 0)
            {
                return false;
            }

            // 最後の part に丸め誤差を吸収させ、合計を必ず車両長へ合わせる
            // Let the last part absorb rounding error so the sum always matches car length
            var assignedLength = 0;
            for (var i = 0; i < authoredPartLengths.Count; i++)
            {
                var isLast = i == authoredPartLengths.Count - 1;
                var normalized = isLast ? carLength - assignedLength : (int)Math.Round(carLength * (authoredPartLengths[i] / (double)authoredTotal));
                if (normalized <= 0)
                {
                    return false;
                }
                normalizedPartLengths[i] = normalized;
                assignedLength += normalized;
            }

            partCount = authoredPartLengths.Count;
            return assignedLength == carLength;
        }

        // model front 基準の part span を、現在の rail head 基準 offset へ写像する
        // Map a model-front-based part span to the current rail-head-based offsets
        public static bool TryBuildPartSpan(int carFrontOffset, int carRearOffset, int partStartOffset, int partLength, bool isFacingForward, out TrainCarPartSpan span)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            span = default;
            var carLength = carRearOffset - carFrontOffset;
            if (carLength <= 0 || partStartOffset < 0 || partLength <= 0)
            {
                return false;
            }
            if (partStartOffset + partLength > carLength)
            {
                return false;
            }

            // reverse 後も見た目の物理位置が変わらないよう model front 座標を反映する
            // Reflect model-front coordinates so visual world placement is stable after reverse
            if (isFacingForward)
            {
                span = new TrainCarPartSpan(carFrontOffset + partStartOffset, carFrontOffset + partStartOffset + partLength);
                return true;
            }
            span = new TrainCarPartSpan(carRearOffset - partStartOffset - partLength, carRearOffset - partStartOffset);
            return true;
        }
    }
}
