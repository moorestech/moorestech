using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.Train.Unit;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UniRx;

namespace Tests.Investigation
{
    public class GameUpdateSubscriberBreakdownInvestigationTest
    {
        private const int WarmupTicks = 100;
        private const int MeasureTicks = 200;

        [Test]
        public void ProfileCurrentSaveTopLevelSubscribers()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);
            var machineOutputs = CollectMachineOutputTargets().Select(target => target.Output).ToArray();

            // BlockSystem 以外の主な subscriber を個別に測る
            // Measure major non-BlockSystem subscribers individually.
            MeasureAndLog("GearNetworkDatastore", () => UpdateGearNetworks(environment.ServiceProvider));
            MeasureAndLog("MachineOutputInventories", () => UpdateMachineOutputInventories(machineOutputs));
            MeasureAndLog("EnergySegments", () => UpdateEnergySegments(environment.ServiceProvider));
            MeasureAndLog("TrainUpdateService", () => InvokePrivate(environment.ServiceProvider.GetService<TrainUpdateService>(), "UpdateTrains"));
            MeasureAndLog("ChallengeDatastore", () => InvokePrivate(environment.ServiceProvider.GetService<Game.Challenge.ChallengeDatastore>(), "Update", Unit.Default));
        }

        [Test]
        public void ProfileCurrentSaveGearAndMachineOutputDetails()
        {
            using var environment = GameUpdatePerformanceTestEnvironment.CreateCurrentSave();
            RunTicks(WarmupTicks);

            // 大きい subscriber をさらに network / block type 単位に分ける
            // Split large subscribers further by network and block type.
            LogGearNetworkDetails(environment.ServiceProvider.GetService<GearNetworkDatastore>());
            LogMachineOutputDetails(CollectMachineOutputTargets());
        }

        private static void UpdateGearNetworks(ServiceProvider serviceProvider)
        {
            var datastore = serviceProvider.GetService<GearNetworkDatastore>();
            foreach (var network in datastore.GearNetworks.Values) network.ManualUpdate();
        }

        private static (string BlockTypeName, VanillaMachineOutputInventory Output)[] CollectMachineOutputTargets()
        {
            var field = typeof(VanillaMachineBlockInventoryComponent).GetField("_vanillaMachineOutputInventory", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values
                .SelectMany(data => data.Block.ComponentManager.GetComponents<VanillaMachineBlockInventoryComponent>()
                    .Select(component => (data.Block.BlockMasterElement.BlockType, (VanillaMachineOutputInventory)field.GetValue(component))))
                .ToArray();
        }

        private static void UpdateMachineOutputInventories(VanillaMachineOutputInventory[] outputs)
        {
            for (var i = 0; i < outputs.Length; i++) outputs[i].DebugUpdateForPerformanceProbe();
        }

        private static void UpdateEnergySegments(ServiceProvider serviceProvider)
        {
            var datastore = serviceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            var method = typeof(EnergySegment).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            for (var i = 0; i < datastore.GetEnergySegmentListCount(); i++)
                method.Invoke(datastore.GetEnergySegment(i), Array.Empty<object>());
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, Array.Empty<object>());
        }

        private static void InvokePrivate(object target, string methodName, object argument)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, new[] { argument });
        }

        private static void MeasureAndLog(string name, Action action)
        {
            var samples = MeasureSamples(MeasureTicks, action);
            var statistics = new GameUpdatePerformanceStatistics(samples);
            UnityEngine.Debug.Log($"[GameUpdateProfile] SubscriberBreakdown name={name} {statistics.ToLogFields()}");
        }

        private static void LogGearNetworkDetails(GearNetworkDatastore datastore)
        {
            var index = 0;
            foreach (var network in datastore.GearNetworks.Values)
            {
                var samples = MeasureSamples(MeasureTicks, network.ManualUpdate);
                var statistics = new GameUpdatePerformanceStatistics(samples);
                UnityEngine.Debug.Log($"[GameUpdateProfile] GearNetworkBreakdown index={index} transformers={network.GearTransformers.Count} generators={network.GearGenerators.Count} {statistics.ToLogFields()}");
                index++;
            }
        }

        private static void LogMachineOutputDetails((string BlockTypeName, VanillaMachineOutputInventory Output)[] targets)
        {
            foreach (var group in targets.GroupBy(target => target.BlockTypeName).OrderByDescending(group => group.Count()))
            {
                var outputs = group.Select(target => target.Output).ToArray();
                var samples = MeasureSamples(MeasureTicks, () => UpdateMachineOutputInventories(outputs));
                var statistics = new GameUpdatePerformanceStatistics(samples);
                UnityEngine.Debug.Log($"[GameUpdateProfile] MachineOutputBreakdown blockType={group.Key} outputs={outputs.Length} {statistics.ToLogFields()}");
            }
        }

        private static List<double> MeasureSamples(int count, Action action)
        {
            var samples = new List<double>(count);
            for (var i = 0; i < count; i++) samples.Add(MeasureMilliseconds(action));
            return samples;
        }

        private static double MeasureMilliseconds(Action action)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            var end = Stopwatch.GetTimestamp();
            return (end - start) * 1000d / Stopwatch.Frequency;
        }

        private static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.Update();
        }
    }
}
