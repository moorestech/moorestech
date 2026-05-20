using System;
using Core.Master;
using Core.Update;
using Game.Train.RailCalc;
using UnityEngine.Windows;

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
                
            }
            var updatedSign = Math.Sign(speed);
            if (sign != updatedSign)
            {
                speed = 0;
            }
            
            // 空気抵抗
            var resistForce = Math.Abs(speed) * MasterHolder.TrainUnitMaster.SpeedWeight * MasterHolder.TrainUnitMaster.Friction +
                              speed * speed * MasterHolder.TrainUnitMaster.SpeedWeight * MasterHolder.TrainUnitMaster.AirResistance;
            var resistSign = Math.Sign(speed);
            speed -= resistSign * resistForce;
            var postResistSign = Math.Sign(speed);
            if (resistSign != postResistSign)
            {
                speed = 0;
            }
            
            var distanceMeters = speed * GameUpdater.SecondsPerTick;
            var floatDistance = distanceMeters * BezierUtility.RAIL_LENGTH_SCALE;
            var accumulated = input.AccumulatedDistance + floatDistance;
            var distance = (int)Math.Truncate(accumulated);
            accumulated -= distance;
            
            return new TrainMotionStepResult(speed, accumulated, distance);
        }
    }
}
