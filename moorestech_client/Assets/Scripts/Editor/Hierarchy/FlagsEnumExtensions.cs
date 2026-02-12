using System;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Abelia.Editor
{
    /// <summary>
    /// Flags属性付きEnum型の拡張メソッドを提供するクラス
    /// </summary>
    public static class FlagsEnumExtensions
    {
        private const string DefaultSeparator = "|";
        
        /// <summary>
        /// Flags属性のenumを見やすくString化します。
        /// デバッグ用なので最適化は考えてません。
        /// C#のenum周りはパフォーマンスが非常に悪いことで有名なので、手を入れたいなら適当な外部ライブラリ導入を推奨します。
        /// </summary>
        /// <param name="value">評価するEnumの値</param>
        /// <param name="separator">Enum名を連結する文字</param>
        /// <typeparam name="T">Enumの型</typeparam>
        /// <returns>フラグの文字列表現</returns>
        public static string ToPrettyStringFromFlags<T>(this T value, string separator = DefaultSeparator) 
            where T : Enum
        {
            var enumType = typeof(T);
            ValidateEnumType(enumType);
            
            var intValue = Convert.ToInt32(value);
            var flagNames = GetFlagNames(enumType, intValue);
            
            return string.Join(separator, flagNames);
        }

        /// <summary>
        /// 高速なフラグチェックを行います
        /// </summary>
        /// <param name="baseValue">チェック対象の値</param>
        /// <param name="compareValue">比較するフラグ値</param>
        /// <typeparam name="T">Enumの型</typeparam>
        /// <returns>フラグが含まれているかどうか</returns>
        public static bool HasFlagFast<T>(this T baseValue, T compareValue) 
            where T : struct, Enum
        {
            var baseInt = UnsafeUtility.EnumToInt(baseValue);
            var compareInt = UnsafeUtility.EnumToInt(compareValue);
            
            return (baseInt & compareInt) != 0;
        }
        
        private static void ValidateEnumType(Type enumType)
        {
            if (Enum.GetUnderlyingType(enumType) != typeof(int))
            {
                Debug.LogWarning($"Enum base type is not int. type: {enumType}");
            }
        }
        
        private static string[] GetFlagNames(Type enumType, int intValue)
        {
            return Enum.GetNames(enumType)
                .Where(name => IsFlagSet(enumType, name, intValue))
                .ToArray();
        }
        
        private static bool IsFlagSet(Type enumType, string flagName, int value)
        {
            var flagValue = Convert.ToInt32(Enum.Parse(enumType, flagName));
            return (value & flagValue) != 0;
        }
    }
}
