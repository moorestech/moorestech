using System;
using Game.Block.Interface.Component;

namespace Game.Block.Component
{
    public static class BlockConnectorOptionReader
    {
        public static GearConnectOptionData? ReadGearOption(object option)
        {
            if (option == null) return null;

            // ギア接続のオプション値を読み取る
            // Read gear connection option values
            var isReverse = GetBool(option, "IsReverse", true);
            return new GearConnectOptionData(isReverse);
        }

        public static FluidConnectOptionData? ReadFluidOption(object option)
        {
            if (option == null) return null;

            // 流体接続のオプション値を読み取る
            // Read fluid connection option values
            var flowCapacity = GetDouble(option, "FlowCapacity", 10d);
            var connectTankIndex = GetInt(option, "ConnectTankIndex", 0);
            return new FluidConnectOptionData(flowCapacity, connectTankIndex);
        }

        #region Internal

        private static bool GetBool(object option, string propertyName, bool fallback)
        {
            var value = GetValue(option, propertyName);
            if (value is bool boolValue) return boolValue;
            if (value is int intValue) return intValue != 0;
            if (value is float floatValue) return Math.Abs(floatValue) > float.Epsilon;
            if (value is double doubleValue) return Math.Abs(doubleValue) > double.Epsilon;
            return fallback;
        }

        private static double GetDouble(object option, string propertyName, double fallback)
        {
            var value = GetValue(option, propertyName);
            if (value is double doubleValue) return doubleValue;
            if (value is float floatValue) return floatValue;
            if (value is int intValue) return intValue;
            if (value is long longValue) return longValue;
            return fallback;
        }

        private static int GetInt(object option, string propertyName, int fallback)
        {
            var value = GetValue(option, propertyName);
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (value is short shortValue) return shortValue;
            if (value is byte byteValue) return byteValue;
            if (value is double doubleValue) return (int)doubleValue;
            if (value is float floatValue) return (int)floatValue;
            return fallback;
        }

        private static object GetValue(object option, string propertyName)
        {
            var property = option.GetType().GetProperty(propertyName);
            if (property != null) return property.GetValue(option);
            var field = option.GetType().GetField(propertyName);
            return field?.GetValue(option);
        }

        #endregion
    }
}
