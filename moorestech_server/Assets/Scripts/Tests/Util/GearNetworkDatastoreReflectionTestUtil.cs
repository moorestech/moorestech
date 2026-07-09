using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Block.Interface;
using Game.Gear.Common;
using NUnit.Framework;

namespace Tests.Util
{
    // GearNetworkDatastoreの内部topologyを、flushなしでテストから読む
    // Reads GearNetworkDatastore internals from tests without triggering a flush
    public static class GearNetworkDatastoreReflectionTestUtil
    {
        private const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        public static IReadOnlyDictionary<GearNetworkId, GearNetwork> GetNetworksWithoutFlush(GearNetworkDatastore datastore)
        {
            var topologyMap = GetRequiredField<object>(datastore, "_topologyMap");
            return GetRequiredField<Dictionary<GearNetworkId, GearNetwork>>(topologyMap, "_gearNetworks");
        }

        public static GearNetwork GetSingleNetworkWithoutFlush(GearNetworkDatastore datastore)
        {
            return GetNetworksWithoutFlush(datastore).Single().Value;
        }

        public static int GetNetworkCountWithoutFlush(GearNetworkDatastore datastore)
        {
            return GetNetworksWithoutFlush(datastore).Count;
        }

        public static GearNetwork GetAppliedNetwork(BlockInstanceId blockInstanceId)
        {
            Assert.True(GearNetworkDatastore.TryGetGearNetwork(blockInstanceId, out var network));
            return network;
        }

        private static T GetRequiredField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceFlags);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
