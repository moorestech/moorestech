using System;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.TickSynchronization
{
    // handlerからcache/viewまで成功経路を省略せずに接続する。
    // Wire the complete successful handler-to-cache/view path without null appliers.
    internal sealed class TrainSnapshotClientFixture : IDisposable
    {
        private readonly GameObject _root = new("TrainSnapshotClientFixture");
        private readonly ClientStationReferenceRegistry _stations;
        public readonly TrainTickContext Context = new();
        public readonly RailGraphClientCache Rails;
        public readonly TrainUnitClientCache Trains;
        public readonly TrainCarObjectDatastore Views;
        public readonly TrainFullSnapshotEventNetworkHandler Handler;
        public readonly TrainUnitHashVerifier Gate;

        public TrainSnapshotClientFixture()
        {
            Rails = (RailGraphClientCache)Activator.CreateInstance(typeof(RailGraphClientCache), true);
            Trains = new TrainUnitClientCache(Rails);
            Views = _root.AddComponent<TrainCarObjectDatastore>();
            Views.Construct();
            _stations = new ClientStationReferenceRegistry(_root.AddComponent<BlockGameObjectDataStore>(), Rails);
            _stations.Initialize();
            Handler = new TrainFullSnapshotEventNetworkHandler(new RailGraphSnapshotApplier(Rails, _stations, Context.State),
                new TrainUnitSnapshotApplier(Trains, Context.State, Views), Context.Events, Context);
            Gate = new TrainUnitHashVerifier(Context.Hashes, Trains, Rails, Context.State);
        }

        public static void Receive(object handler, string methodName, byte[] payload)
        {
            // 既存失敗伝搬fixtureと同じ受信境界を使う。
            // Invoke the same receive boundary used by the existing failure-propagation fixture.
            var method = handler.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(handler, new object[] { payload });
        }

        public void ApplyRail(byte[] payload) => Receive(Handler, "HandleRailGraphFullSnapshot", payload);
        public void ApplyTrain(byte[] payload) => Receive(Handler, "HandleTrainUnitFullSnapshot", payload);

        public void Dispose()
        {
            Handler.Dispose();
            _stations.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
