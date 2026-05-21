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
            int totalWeight,
            double totalTraction)
        {
            CurrentSpeed = currentSpeed;
            RemainingDistance = remainingDistance;
            TotalWeight = totalWeight;
            TotalTraction = totalTraction;
        }

        public double CurrentSpeed { get; }
        public int RemainingDistance { get; }
        public int TotalWeight { get; }
        public double TotalTraction { get; }
    }

    /// <summary>
    /// AutoRun計算処理
    /// Static helper for auto-run mascon calculation
    /// </summary>
    public static class TrainAutoRunMasconCalculator
    {
        private const double TargetBrakeRate = 7.0d / 8.0d;
        private const double MaxBrakeRate = 1.0d;
        private const double AutoSpeedTrackingGain = 1.25d;

        public static int Calculate(in AutoRunMasconInput input)
        {
            var maxMascon = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var speed = Math.Abs(input.CurrentSpeed);
            // 手前で速度0停止しても再加速できるようしないようマージンをセット
            var remainingMeters = Math.Max(1, BezierUtility.RAIL_LENGTH_SCALE / 4 + input.RemainingDistance) / (double)BezierUtility.RAIL_LENGTH_SCALE;
            
            var maxBrakeAcceleration = MasterHolder.TrainUnitMaster.MaxBrakeDecelerationMetersPerSecondSquared;
            if (maxBrakeAcceleration <= 0)
            {
                return maxMascon;
            }

            var totalWeight = input.TotalWeight;
            // 残距離から7/8ブレーキ曲線上の許容速度を求める。
            // Calculate the allowed speed on the 7/8 brake curve from the remaining distance.
            var resistanceAcceleration = TrainDistanceSimulator.CalculateResistanceAcceleration(speed, totalWeight);
            var curveAcceleration = maxBrakeAcceleration * TargetBrakeRate + resistanceAcceleration;
            var allowedSpeed = Math.Sqrt(2.0d * curveAcceleration * remainingMeters);
            // 許容速度との差を1tick分の加速度へ変換し、曲線が要求する再加速も許可する。
            // Convert the speed gap into per-tick acceleration and allow curve-driven re-acceleration.
            var targetAcceleration = (allowedSpeed - speed) / GameUpdater.SecondsPerTick * AutoSpeedTrackingGain;

            if (targetAcceleration >= 0)
            {
                var maxTractionAcceleration = input.TotalTraction / totalWeight;
                if (maxTractionAcceleration <= 0)
                {
                    return 0;
                }
                var tractionRate = Math.Min(targetAcceleration / maxTractionAcceleration, 1.0d);
                return (int)Math.Round(maxMascon * tractionRate);
            }

            // 曲線が減速を要求する場合だけ、必要量を最大ブレーキまで出す。
            // Apply braking only when the curve requires deceleration, capped at full brake.
            var brakeRate = Math.Min(-targetAcceleration / maxBrakeAcceleration, MaxBrakeRate);
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
