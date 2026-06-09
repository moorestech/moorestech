using System;

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
        // model front 基準の比率spanを、現在の rail head 基準 offset へ写像する
        // Map a model-front-based ratio span to the current rail-head-based offsets
        public static bool TryBuildPartSpanByRatio(int carFrontOffset, int carRearOffset, float partFrontRatio, float partRearRatio, bool isFacingForward, out TrainCarPartSpan span)
        {
            // 出力値を先に初期化し、比率指定の妥当性を検証する
            // Initialize output values first and validate the ratio range
            span = default;
            var carLength = carRearOffset - carFrontOffset;
            if (carLength < 0 || partFrontRatio < 0f || partRearRatio > 1f || partFrontRatio >= partRearRatio)
            {
                return false;
            }
            if (float.IsNaN(partFrontRatio) || float.IsNaN(partRearRatio) || float.IsInfinity(partFrontRatio) || float.IsInfinity(partRearRatio))
            {
                return false;
            }

            // 比率を親span内の整数offsetへ丸め、既存の向き反映ロジックへ渡す
            // Round ratios into integer offsets inside the parent span and reuse existing facing logic
            var partStartOffset = (int)Math.Round(carLength * partFrontRatio);
            var partEndOffset = (int)Math.Round(carLength * partRearRatio);
            return TryBuildPartSpan(carFrontOffset, carRearOffset, partStartOffset, partEndOffset - partStartOffset, isFacingForward, out span);
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
