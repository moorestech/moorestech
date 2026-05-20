using System;
using Core.Master;
using Core.Update;
using Game.Train.RailCalc;

namespace Game.Train.Unit
{
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
            var maxSpeed = Math.Sqrt(remaining * MasterHolder.TrainUnitMaster.AutoRunMaxSpeedDistanceCoefficient) + MasterHolder.TrainUnitMaster.AutoRunMaxSpeedOffset;
            if (maxSpeed > input.CurrentSpeed)
            {
                mascon = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            }

            var bufferedSpeed = input.CurrentSpeed * MasterHolder.TrainUnitMaster.AutoRunSpeedBufferRate;
            if (maxSpeed < bufferedSpeed)
            {
                var subspeed = maxSpeed - bufferedSpeed;
                mascon = Math.Max((int)subspeed, -MasterHolder.TrainUnitMaster.MasconLevelMaximum);
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
            double totalTraction,
            int totalweight)
        {
            CurrentSpeed = currentSpeed;
            AccumulatedDistance = accumulatedDistance;
            MasconLevel = masconLevel;
            TotalTraction = totalTraction;
            TotalWeight = totalweight;
        }
        public double CurrentSpeed { get; }
        public double AccumulatedDistance { get; }
        public int MasconLevel { get; }
        public double TotalTraction { get; }
        public int TotalWeight { get; }
        
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
    /// 速度と進行距離のシミュレーション、空気抵抗、摩擦
    /// Static helper for per-tick distance simulation
    /// </summary>
    public static class TrainDistanceSimulator
    {
        public static TrainMotionStepResult Step(in TrainMotionStepInput input)
        {
            var speed = input.CurrentSpeed;
            var sign = Math.Sign(speed);
            if (input.MasconLevel > 0)
            {
                var masconRate = input.MasconLevel / (double)MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                var acceleration = input.TotalTraction / input.TotalWeight * masconRate;
                speed += acceleration * GameUpdater.SecondsPerTick;
            }
            if (input.MasconLevel < 0)
            {
                var brakeRate = -input.MasconLevel / (double)MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                var brakeAcceleration = MasterHolder.TrainUnitMaster.ManualControlDecelerationFactor * brakeRate;
                speed = ApplyOpposingAcceleration(speed, brakeAcceleration, GameUpdater.SecondsPerTick);
            }
            var updatedSign = Math.Sign(speed);
            if (sign != updatedSign)
            {
                speed = 0;
            }
            
            var resistanceAcceleration = CalculateResistanceAcceleration(speed, input.TotalWeight);
            speed = ApplyOpposingAcceleration(speed, resistanceAcceleration, GameUpdater.SecondsPerTick);
            
            var distanceMeters = speed * GameUpdater.SecondsPerTick;
            var floatDistance = distanceMeters * BezierUtility.RAIL_LENGTH_SCALE;
            var accumulated = input.AccumulatedDistance + floatDistance;
            var distance = (int)Math.Truncate(accumulated);
            accumulated -= distance;
            return new TrainMotionStepResult(speed, accumulated, distance);
            
    #region Internal
            
            static double CalculateResistanceAcceleration(double speed, int totalWeight)
            {
                if (speed == 0) return 0;
                var rollingResistanceForce = MasterHolder.TrainUnitMaster.Friction * totalWeight * 9.80665;
                var airResistanceForce = MasterHolder.TrainUnitMaster.AirResistance * speed * speed;
                var resistanceForce = rollingResistanceForce + airResistanceForce;
                return resistanceForce / totalWeight;
            }
            
            static double ApplyOpposingAcceleration(double speed, double acceleration, double secondsPerTick)
            {
                if (speed == 0 || acceleration <= 0) return speed;
                var sign = Math.Sign(speed);
                var nextSpeed = speed - sign * acceleration * secondsPerTick;
                return sign != Math.Sign(nextSpeed) ? 0 : nextSpeed;
            }
            
    #endregion
        }
    }
}
