using System;
using System.Reflection;
using Client.Input;

namespace Client.Tests.Common
{
    /// <summary>
    ///     テスト横断で共有する非公開メンバー操作
    ///     Private-member access shared across tests
    /// </summary>
    internal static class TestReflection
    {
        public static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        public static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
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
