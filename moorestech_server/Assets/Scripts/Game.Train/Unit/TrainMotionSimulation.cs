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
            int remainingDistance,
            int totalWeight)
        {
            CurrentSpeed = currentSpeed;
            RemainingDistance = remainingDistance;
            TotalWeight = totalWeight;
        }

        public double CurrentSpeed { get; }
        public int RemainingDistance { get; }
        public int TotalWeight { get; }
    }

    /// <summary>
    /// AutoRun計算処理
    /// Static helper for auto-run mascon calculation
    /// </summary>
    public static class TrainAutoRunMasconCalculator
    {
        public static int Calculate(in AutoRunMasconInput input)
        {
            /* 
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
            */
            
            const double TargetBrakeRate = 7.0d / 8.0d;
            var maxMascon = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var speed = Math.Abs(input.CurrentSpeed);
            var remainingMeters = Math.Max(0, input.RemainingDistance) / (double)BezierUtility.RAIL_LENGTH_SCALE;
            
            if (remainingMeters <= 0)
            {
                return speed > 0 ? -(int)Math.Round(maxMascon * TargetBrakeRate) : 0;
            }
            
            var maxBrakeAcceleration = MasterHolder.TrainUnitMaster.MaxBrakeDecelerationMetersPerSecondSquared;
            
// ここは本当は totalWeight が必要。
// airResistance は weight で割って加速度にする必要がある。
            var resistanceAcceleration = TrainDistanceSimulator.CalculateResistanceAcceleration(speed, input.TotalWeight);
            var requiredTotalDeceleration = speed * speed / (2.0d * remainingMeters);
            var requiredBrakeAcceleration = Math.Max(0, requiredTotalDeceleration - resistanceAcceleration);
            var requiredBrakeRate = requiredBrakeAcceleration / maxBrakeAcceleration;
// まだ 7/8 ブレーキ曲線に入っていないなら加速。
// ここで弱いブレーキを遠くから入れない。
            if (requiredBrakeRate < TargetBrakeRate)
            {
                return maxMascon;
            }
// 曲線に入ったら、必要量を毎tick再計算する。
// 通常は 7/8 付近に張り付く。
            var brakeRate = Math.Min(requiredBrakeRate, TargetBrakeRate);
            return -(int)Math.Round(maxMascon * brakeRate);
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
                var brakeRate = Math.Min(Math.Abs((double)input.MasconLevel) / MasterHolder.TrainUnitMaster.MasconLevelMaximum, 1.0d);
                var brakeAcceleration = MasterHolder.TrainUnitMaster.MaxBrakeDecelerationMetersPerSecondSquared * brakeRate;
                speed = ApplyOpposingAcceleration(speed, brakeAcceleration, GameUpdater.SecondsPerTick);
            }
            var updatedSign = Math.Sign(speed);
            if (sign != 0 && sign != updatedSign)
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
            static double ApplyOpposingAcceleration(double speed, double acceleration, double secondsPerTick)
            {
                if (speed == 0 || acceleration <= 0) return speed;
                var sign = Math.Sign(speed);
                var nextSpeed = speed - sign * acceleration * secondsPerTick;
                return sign != Math.Sign(nextSpeed) ? 0 : nextSpeed;
            }
            #endregion
        }
        
        public static double CalculateResistanceAcceleration(double speed, int totalWeight)
        {
            if (speed == 0) return 0;
            var rollingResistanceForce = MasterHolder.TrainUnitMaster.Friction * totalWeight * 9.80665;
            var airResistanceForce = MasterHolder.TrainUnitMaster.AirResistance * speed * speed;
            var resistanceForce = rollingResistanceForce + airResistanceForce;
            return resistanceForce / totalWeight;
        }

    }
}
