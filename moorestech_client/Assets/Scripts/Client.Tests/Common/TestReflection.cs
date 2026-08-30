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

        // 他セッションの入力アセットは張り直す
        // InputTestFixture swaps the InputSystem, so an input asset built in another session must be dropped and rebuilt
        public static void ResetInputManagerCache()
        {
            // 有効なまま捨てるとファイナライザがリーク警告を出し無関係なテストを落とすので先に止める
            // Dropping it while enabled makes the finalizer log a leak assert that fails an unrelated test, so stop it first
            var instanceField = typeof(InputManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            ((MoorestechInputSettings)instanceField.GetValue(null))?.Disable();

            foreach (var fieldName in new[] { "_instance", "player", "playable", "ui" })
            {
                typeof(InputManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }
    }
}
