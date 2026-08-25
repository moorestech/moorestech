using System;
using System.Reflection;
using Client.Input;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     採掘テストが共有する非公開メンバー操作
    ///     Private-member access shared by the mining tests
    /// </summary>
    internal static class MiningTestReflection
    {
        public static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        public static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        public static void SetStaticProperty(Type targetType, string propertyName, object value)
        {
            targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        // InputTestFixtureがInputSystemを差し替えるため、他セッションの入力アセットは捨てて張り直す
        // InputTestFixture swaps the InputSystem, so an input asset built in another session must be dropped and rebuilt
        public static void ResetInputManagerCache()
        {
            foreach (var fieldName in new[] { "_instance", "player", "playable", "ui" })
            {
                typeof(InputManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }
    }
}
