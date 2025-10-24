using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainSpeedSaveLoadTest
    {

        [Test]
        public void SingleHighSpeedTrainPreservesSpeedAcrossSaveLoad()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var train = scenario.CreateForwardDockingTrain(out var car);
            //train.の_currentSpeedを349431.247に設定    
            //リフレクションでprivateフィールドにアクセス
            const double expectedSpeed = 349431.247;
            SetPrivateField(train, "_currentSpeed", expectedSpeed);
            //速度assert
            Assert.AreEqual(expectedSpeed, train.CurrentSpeed, 1e-10, "列車速度の設定に失敗しました。");
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(scenario.Environment.ServiceProvider);

            scenario.Dispose();

            var loadEnv = TrainTestHelper.CreateEnvironment();
            SaveLoadJsonTestHelper.LoadFromJson(loadEnv.ServiceProvider, saveJson);

            var loadedTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            Assert.AreEqual(1, loadedTrains.Count, "ロード後の登録列車数が一致しません。");

            var loadedTrain = loadedTrains[0];
            var loadedCar = loadedTrain.Cars[0];
            Assert.AreEqual(expectedSpeed, loadedTrain.CurrentSpeed, 1e-10, "ロード後の列車速度が一致しません。");

            CleanupTrains(loadedTrains);
        }

        private static void CleanupTrains(IEnumerable<TrainUnit> trains)
        {
            foreach (var train in trains)
            {
                train.trainUnitStationDocking.UndockFromStation();
                TrainDiagramManager.Instance.UnregisterDiagram(train.trainDiagram);
                TrainUpdateService.Instance.UnregisterTrain(train);
            }
        }

        private static void SetPrivateField<T>(TrainUnit train, string fieldName, T value)
        {
            var field = typeof(TrainUnit).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} フィールドを取得できませんでした。");
            field!.SetValue(train, value);
        }

    }
}
