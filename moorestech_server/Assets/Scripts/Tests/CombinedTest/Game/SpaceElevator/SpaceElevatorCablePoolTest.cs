using System;
using System.Text.RegularExpressions;
using Game.Orbit;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorCablePoolTest
    {
        [Test]
        public void ケーブルの到着だけがプールを増やす()
        {
            var provider = CreateServer();
            var orbital = provider.GetService<IOrbitalArrivalService>();
            var pool = provider.GetService<SpaceElevatorCablePool>();

            orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
            orbital.Arrive(ForUnitTestItemId.TestElevatorSatellite, 1);

            Assert.AreEqual(50, pool.Amount);
        }

        [Test]
        public void 成果の問い合わせでは到着の通知が流れずプールも増えない()
        {
            var provider = CreateServer();
            var orbital = provider.GetService<IOrbitalArrivalService>();
            var pool = provider.GetService<SpaceElevatorCablePool>();
            var notified = 0;
            orbital.OnOrbitalArrival.Subscribe(_ => notified++);

            // 昇降機は出口待ちの間 毎tick問い合わせるので、問い合わせに副作用が無いことを確かめる
            // The climber asks every tick while the outlet is full, so the query must have no side effect
            orbital.PreviewArrivalOutputs(ForUnitTestItemId.TestElevatorSatellite, 1);
            orbital.PreviewArrivalOutputs(ForUnitTestItemId.TestElevatorCable, 50);

            Assert.AreEqual(0, notified);
            Assert.AreEqual(0, pool.Amount);
        }

        [Test]
        public void 残量はセーブを往復する()
        {
            var provider = CreateServer();
            provider.GetService<IOrbitalArrivalService>().Arrive(ForUnitTestItemId.TestElevatorCable, 100);
            var json = provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別のDIで読み直すと残量が戻る
            // Reloading through a fresh container restores the amount
            var loadedProvider = CreateServer();
            ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(json);
            Assert.AreEqual(100, loadedProvider.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void 現在版のセーブに節が無ければロードで止まる()
        {
            var provider = CreateServer();
            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            save.Remove("spaceElevatorCablePool");

            var loadedProvider = CreateServer();
            LogAssert.Expect(LogType.Error, new Regex("spaceElevatorCablePool"));
            Assert.Throws<InvalidOperationException>(() => ((WorldLoaderFromJson)loadedProvider.GetService<IWorldSaveDataLoader>()).Load(save));
        }

        private static ServiceProvider CreateServer()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return provider;
        }
    }
}
