using System;
using System.IO;
using Core.Master;
using Core.Update;
using Game.Train.RailCalc;
using Game.Train.Unit.Motion;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.TrainMotion
{
    // 影響度≠1のマスタで加速・空気抵抗・自動運転が実効重量を使うか検証
    // Verifies acceleration, air resistance and auto-run use effective weight under a non-unit exponent
    public class TrainWeightInfluenceSimulationTest
    {
        private const int ReferenceWeight = 40000;
        private const double Exponent = 0.5;
        private const int TotalWeight = ReferenceWeight * 4;

        // 改変マスタを後続テストへ残さないよう通常マスタへ戻す
        // Restore the normal master so the mutated one does not leak into later tests
        [TearDown]
        public void RestoreDefaultMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void StepWithNonUnitExponent_UsesEffectiveWeightForTractionAcceleration()
        {
            LoadMasterWithWeightInfluence(Exponent, ReferenceWeight);

            const double totalTraction = 800000;
            var masconMax = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var input = new TrainMotionStepInput(0, 0, masconMax, totalTraction, TotalWeight);
            var result = TrainDistanceSimulator.Step(input);

            // マスタ値から独立に計算
            // Compute independently from master values
            var effectiveWeight = IndependentEffectiveWeight(TotalWeight, Exponent, ReferenceWeight);
            var afterTraction = totalTraction / effectiveWeight * GameUpdater.SecondsPerTick;
            var expectedResistance = IndependentResistanceAcceleration(afterTraction, effectiveWeight);
            var expectedSpeed = afterTraction - expectedResistance * GameUpdater.SecondsPerTick;

            Assert.AreEqual(expectedSpeed, result.NewSpeed, 1e-9);
        }

        [Test]
        public void CalculateFromMaster_ReadsExponentAndReferenceWeightFromMaster()
        {
            LoadMasterWithWeightInfluence(Exponent, ReferenceWeight);

            var actual = TrainEffectiveWeightCalculator.CalculateFromMaster(TotalWeight);
            var expected = TrainEffectiveWeightCalculator.Calculate(TotalWeight, Exponent, ReferenceWeight);

            Assert.AreEqual(expected, actual, 1e-9);
        }

        [Test]
        public void CalculateResistanceAcceleration_DividesAirResistanceByEffectiveWeight()
        {
            LoadMasterWithWeightInfluence(Exponent, ReferenceWeight);
            const double speed = 20;

            var actual = TrainDistanceSimulator.CalculateResistanceAcceleration(speed, TotalWeight);
            var effectiveWeight = IndependentEffectiveWeight(TotalWeight, Exponent, ReferenceWeight);
            var expected = IndependentResistanceAcceleration(speed, effectiveWeight);

            Assert.AreEqual(expected, actual, 1e-12);
        }

        [Test]
        public void AutoRunMascon_CapsTractionByEffectiveWeightWhenBelowCurve()
        {
            LoadMasterWithWeightInfluence(Exponent, ReferenceWeight);
            const double totalTraction = 20000000;
            const int remainingDistance = 10240;

            var input = new AutoRunMasconInput(0, remainingDistance, TotalWeight, totalTraction);
            var actualMascon = TrainAutoRunMasconCalculator.Calculate(input);

            var effectiveWeight = IndependentEffectiveWeight(TotalWeight, Exponent, ReferenceWeight);
            var expectedMascon = IndependentAutoRunMascon(remainingDistance, totalTraction, effectiveWeight);

            // マスコン未飽和を前提
            // Assumes mascon is not saturated
            Assert.Less(expectedMascon, MasterHolder.TrainUnitMaster.MasconLevelMaximum);
            Assert.AreEqual(expectedMascon, actualMascon);
        }

        [Test]
        public void WeightInfluenceValidation_RejectsNegativeExponentAndNonPositiveReferenceWeight()
        {
            // Validateは車両の必要アイテム検証でItemMasterを引くため通常マスタを先に読む
            // Validate reads ItemMaster for required items, so load the normal master first
            RestoreDefaultMaster();
            var negativeExponentMaster = BuildTrainUnitMasterWithMotionOverride("weightInfluenceExponent", -1);
            Assert.IsFalse(negativeExponentMaster.Validate(out var negativeExponentErrors));
            Assert.IsTrue(negativeExponentErrors.Contains("WeightInfluenceExponent"));

            var nonPositiveReferenceWeightMaster = BuildTrainUnitMasterWithMotionOverride("referenceWeight", 0);
            Assert.IsFalse(nonPositiveReferenceWeightMaster.Validate(out var referenceWeightErrors));
            Assert.IsTrue(referenceWeightErrors.Contains("ReferenceWeight"));
        }

        // motionParametersだけ差し替えロード
        // Overrides only motionParameters, then loads
        private static void LoadMasterWithWeightInfluence(double exponent, int referenceWeight)
        {
            var trainJson = ReadTestTrainJson();
            trainJson["motionParameters"]["weightInfluenceExponent"] = exponent;
            trainJson["motionParameters"]["referenceWeight"] = referenceWeight;

            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            configs[0].JsonContents[new JsonFileName("train")] = trainJson.ToString();
            MasterHolder.Load(new MasterJsonFileContainer(configs));
        }

        // Validateだけを孤立して検証するため、DIコンテナやMasterHolderを経由せず直接構築する
        // Builds a standalone TrainUnitMaster to test Validate in isolation, without going through the DI container or MasterHolder
        private static TrainUnitMaster BuildTrainUnitMasterWithMotionOverride(string key, double value)
        {
            var trainJson = ReadTestTrainJson();
            trainJson["motionParameters"][key] = value;
            return new TrainUnitMaster(trainJson);
        }

        private static JObject ReadTestTrainJson()
        {
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            return JObject.Parse(configs[0].JsonContents[new JsonFileName("train")]);
        }

        private static double IndependentEffectiveWeight(int totalWeight, double exponent, int referenceWeight)
        {
            return referenceWeight * Math.Pow(totalWeight / (double)referenceWeight, exponent);
        }

        private static double IndependentResistanceAcceleration(double speed, double effectiveWeight)
        {
            var rolling = MasterHolder.TrainUnitMaster.Friction * 9.80665;
            var air = MasterHolder.TrainUnitMaster.AirResistance * speed * speed / effectiveWeight;
            return rolling + air;
        }

        private static int IndependentAutoRunMascon(int remainingDistance, double totalTraction, double effectiveWeight)
        {
            var maxMascon = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var remainingMeters = Math.Max(1, BezierUtility.RAIL_LENGTH_SCALE / 6 + remainingDistance) / (double)BezierUtility.RAIL_LENGTH_SCALE;
            var maxBrakeAcceleration = MasterHolder.TrainUnitMaster.MaxBrakeDecelerationMetersPerSecondSquared;
            // speed=0は既定で抵抗0(無関係)
            // speed=0: resistance is 0 by default, unrelated
            var curveAcceleration = maxBrakeAcceleration * 7.0d / 8.0d;
            var allowedSpeed = Math.Sqrt(2.0d * curveAcceleration * remainingMeters);
            var targetAcceleration = allowedSpeed / GameUpdater.SecondsPerTick;
            var maxTractionAcceleration = totalTraction / effectiveWeight;
            var tractionRate = Math.Min(targetAcceleration / maxTractionAcceleration, 1.0d);
            return (int)Math.Round(maxMascon * tractionRate);
        }
    }
}
