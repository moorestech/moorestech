using System;

namespace Game.Train.Unit
{
    /// <summary>
    /// 自動運転用マスコン計算の入力値
    /// Input payload for auto-run mascon calculation
    /// </summary>
    public static class TrainMotionParameters
    {
        public const double Friction = 0.0008;
        public const double AirResistance = 0.0002;
        public const double SpeedWeight = 0.03;
        public const double AutoRunMaxSpeedDistanceCoefficient = 10000.0;
        public const double AutoRunMaxSpeedOffset = 10.0;
        public const double AutoRunSpeedBufferMargin = 0.02;
        public const double AutoRunSpeedBufferRate = 1.0 - AutoRunSpeedBufferMargin;
        public const double TractionForceAccelerationRate = 0.1;
        public const double ManualControlDecelerationFactor = 1.0;
        public const int MasconLevelMaximum = 16777216;
        public const int WEIGHT_PER_SLOT = 40;
        public const int DEFAULT_WEIGHT = 320;
        public const int DEFAULT_TRACTION = 400;
    }

    public readonly struct AutoRunMasconInput
    {
        public AutoRunMasconInput(
            double currentSpeed,
            int remainingDistance)
        {
            CurrentSpeed = currentSpeed;
            RemainingDistance = remainingDistance;
        }

        public double CurrentSpeed { get; }
        public int RemainingDistance { get; }
    }

    /// <summary>
    /// AutoRun計算処理
    /// Static helper for auto-run mascon calculation
    /// </summary>
    public static class TrainAutoRunMasconCalculator
    {
        public static int Calculate(in AutoRunMasconInput input)
        {
            var mascon = 0;
            var remaining = Math.Max(0, input.RemainingDistance);
            var maxSpeed = Math.Sqrt(remaining * TrainMotionParameters.AutoRunMaxSpeedDistanceCoefficient) + TrainMotionParameters.AutoRunMaxSpeedOffset;
            if (maxSpeed > input.CurrentSpeed)
            {
                mascon = TrainMotionParameters.MasconLevelMaximum;
            }

            var bufferedSpeed = input.CurrentSpeed * TrainMotionParameters.AutoRunSpeedBufferRate;
            if (maxSpeed < bufferedSpeed)
            {
                var subspeed = maxSpeed - bufferedSpeed;
                mascon = Math.Max((int)subspeed, -TrainMotionParameters.MasconLevelMaximum);
            }

            return mascon;
        }
    }

    /// <summary>
    /// 速度・距離シミュレーションの入力パラメータ
    /// Input payload for speed/distance simulation
    /// </summary>
    public readonly struct TrainMotionStepInput
    {
        public TrainMotionStepInput(
            double currentSpeed,
            double accumulatedDistance,
            int masconLevel,
            double tractionForce)
        {
            CurrentSpeed = currentSpeed;
            AccumulatedDistance = accumulatedDistance;
            MasconLevel = masconLevel;
            TractionForce = tractionForce;
        }

        public double CurrentSpeed { get; }
        public double AccumulatedDistance { get; }
        public int MasconLevel { get; }
        public double TractionForce { get; }
    }

    /// <summary>
    /// 速度と距離計算結果
    /// Result for motion step simulation
    /// </summary>
    public readonly struct TrainMotionStepResult
    {
        public TrainMotionStepResult(double newSpeed, double newAccumulatedDistance, int distanceToMove)
        {
            NewSpeed = newSpeed;
            NewAccumulatedDistance = newAccumulatedDistance;
            DistanceToMove = distanceToMove;
        }

        public double NewSpeed { get; }
        public double NewAccumulatedDistance { get; }
        public int DistanceToMove { get; }
    }

    /// <summary>
    /// 速度と進行距離のシミュレーション
    /// Static helper for per-tick distance simulation
    /// </summary>
    public static class TrainDistanceSimulator
    {
        public static TrainMotionStepResult Step(in TrainMotionStepInput input)
        {
            var speed = input.CurrentSpeed;
            if (input.MasconLevel > 0)
            {
                speed += input.TractionForce * TrainMotionParameters.TractionForceAccelerationRate;
            }
            else
            {
                var sign = Math.Sign(speed);
                speed += sign * input.MasconLevel * TrainMotionParameters.ManualControlDecelerationFactor;
                var updatedSign = Math.Sign(speed);
                if (sign != updatedSign)
                {
                    speed = 0;
                }
            }

            var resistForce = Math.Abs(speed) * TrainMotionParameters.SpeedWeight * TrainMotionParameters.Friction +
                              speed * speed * TrainMotionParameters.SpeedWeight * TrainMotionParameters.AirResistance;
            var resistSign = Math.Sign(speed);
            speed -= resistSign * resistForce;
            var postResistSign = Math.Sign(speed);
            if (resistSign != postResistSign)
            {
                speed = 0;
            }

            var floatDistance = speed * TrainMotionParameters.SpeedWeight;
            var accumulated = input.AccumulatedDistance + floatDistance;
            var distance = (int)Math.Truncate(accumulated);
            accumulated -= distance;
            return new TrainMotionStepResult(speed, accumulated, distance);
        }
    }
}
