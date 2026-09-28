using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.RailGraph;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.TrainSynchronization
{
    internal sealed class TrainSynchronizationTestContext : IDisposable
    {
        internal readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "train-stall-tests", Guid.NewGuid().ToString("N"));
        internal readonly TrainUnitTickState State = new();
        internal readonly RailGraphClientCache Rail = RailGraphClientCache.CreateForEditorTest();
        internal readonly TrainUnitClientCache Trains;
        internal readonly TrainSynchronizationDiagnostics Diagnostics;
        internal readonly TrainUnitFutureMessageBuffer Buffer;
        internal readonly TrainUnitHashVerifier Gate;
        internal readonly TrainUnitClientSimulator Simulator;
        internal readonly List<string> Warnings = new();
        private GameObject _snapshotObjects;

        internal TrainSynchronizationTestContext()
        {
            // 空の実キャッシュと実gateを結び、ネットワーク以降の経路を共有する。
            // Connect real empty caches and gate to share the path downstream of network delivery.
            Trains = new TrainUnitClientCache(Rail);
            Diagnostics = new TrainSynchronizationDiagnostics(State, new TrainSynchronizationDiagnosticWriter(DirectoryPath));
            Buffer = new TrainUnitFutureMessageBuffer(State, Diagnostics);
            Gate = new TrainUnitHashVerifier(Buffer, Trains, Rail, State, Diagnostics);
            var visuals = new TrainUnitVisualUpdateSystem(Trains, null, Rail);
            Simulator = new TrainUnitClientSimulator(State, Gate, Buffer, visuals, Diagnostics);
            Application.logMessageReceived += CaptureWarning;
        }

        internal void InitializeDiagnostics(uint tick)
        {
            State.RecordAppliedTickUnifiedId(tick, 0);
            Diagnostics.Initialize(State.GetAppliedTickUnifiedId());
        }

        internal TrainFullSnapshotEventNetworkHandler CreateSnapshotHandler()
        {
            _snapshotObjects = new GameObject("Train synchronization snapshot test");
            var datastore = _snapshotObjects.AddComponent<TrainCarObjectDatastore>();
            var registry = new ClientStationReferenceRegistry(null, Rail);
            return new TrainFullSnapshotEventNetworkHandler(new RailGraphSnapshotApplier(Rail, registry, State),
                new TrainUnitSnapshotApplier(Trains, State, datastore), Buffer, Diagnostics);
        }

        internal void ApplyInitialSnapshot(uint tick)
        {
            var handler = CreateSnapshotHandler();
            ApplyRailSnapshot(handler, tick);
            ApplyTrainSnapshot(handler, tick);
            handler.WaitForInitialApplyAsync().GetAwaiter().GetResult();
        }

        internal void ApplyRailSnapshot(TrainFullSnapshotEventNetworkHandler handler, uint tick)
        {
            var railSnapshot = new RailGraphSnapshot(Array.Empty<RailNodeInitializationData>(),
                Array.Empty<RailGraphConnectionSnapshot>(), Rail.ComputeCurrentHash(), tick);
            var message = new TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack(new RailGraphSnapshotMessagePack(railSnapshot, 0));
            Deliver(handler, "HandleRailGraphFullSnapshot", MessagePackSerializer.Serialize(message));
        }

        internal void ApplyTrainSnapshot(TrainFullSnapshotEventNetworkHandler handler, uint tick)
        {
            var message = new TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack(
                new List<TrainUnitSnapshotBundleMessagePack>(), tick, Trains.ComputeCurrentHash(), 0);
            Deliver(handler, "HandleTrainUnitFullSnapshot", MessagePackSerializer.Serialize(message));
        }

        internal static void Deliver(TrainFullSnapshotEventNetworkHandler handler, string methodName, byte[] payload)
        {
            typeof(TrainFullSnapshotEventNetworkHandler).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(handler, new object[] { payload });
        }

        internal string[] Reports()
        {
            return Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "*.json") : Array.Empty<string>();
        }

        internal JObject ReadReport()
        {
            var reports = Reports();
            Assert.That(reports, Has.Length.EqualTo(1));
            return JObject.Parse(File.ReadAllText(reports[0]));
        }

        public void Dispose()
        {
            Application.logMessageReceived -= CaptureWarning;
            if (_snapshotObjects != null) UnityEngine.Object.DestroyImmediate(_snapshotObjects);
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }

        private void CaptureWarning(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && message.StartsWith("[TrainSynchronization]")) Warnings.Add(message);
        }
    }
}
