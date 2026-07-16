using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.Util
{
    // EnergySystemの非公開構築APIと適用済み状態をテスト側だけから観測する
    // Observes non-public EnergySystem construction APIs and applied state from tests only
    public static class ElectricNetworkReflectionTestUtil
    {
        private const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        public static object GetTopologyMap(IElectricWireNetworkLookup datastore)
        {
            return GetRequiredField<object>(datastore, "_topologyMap");
        }

        public static int GetSegmentCount(IElectricWireNetworkLookup datastore)
        {
            var map = GetTopologyMap(datastore);
            return GetRequiredField<ICollection>(map, "_segments").Count;
        }

        public static bool IsTopologyDirty(ElectricWireNetworkDatastore datastore)
        {
            return GetRequiredField<bool>(datastore, "_isTopologyDirty");
        }

        public static IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> GetConsumers(EnergySegment segment)
        {
            return GetRequiredField<Dictionary<BlockInstanceId, IElectricConsumer>>(segment, "_consumers");
        }

        public static IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> GetGenerators(EnergySegment segment)
        {
            return GetRequiredField<Dictionary<BlockInstanceId, IElectricGenerator>>(segment, "_generators");
        }

        public static EnergySegment CreateSegment()
        {
            var constructor = typeof(EnergySegment).GetConstructor(InstanceFlags, null, Type.EmptyTypes, null);
            if (constructor == null) throw new MissingMethodException(typeof(EnergySegment).FullName, ".ctor()");
            return (EnergySegment)constructor.Invoke(Array.Empty<object>());
        }

        public static void AddGenerator(EnergySegment segment, IElectricGenerator generator)
        {
            Invoke(segment, "AddGenerator", generator);
        }

        public static void AddEnergyConsumer(EnergySegment segment, IElectricConsumer consumer)
        {
            Invoke(segment, "AddEnergyConsumer", consumer);
        }

        public static ElectricNetworkStatistics SettleTick(EnergySegment segment)
        {
            return (ElectricNetworkStatistics)Invoke(segment, "SettleTick");
        }

        private static T GetRequiredField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceFlags);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            return (T)field.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, InstanceFlags);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, methodName);
            return method.Invoke(target, arguments);
        }
    }
}
