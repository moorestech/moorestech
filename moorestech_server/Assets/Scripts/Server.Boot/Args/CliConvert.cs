#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server.Boot.Args;

namespace Server.Boot.Args
{
    /// <summary>string[] ⇆ POCO を行き来させる最小実装。</summary>
    public static class CliConvert
    {
        /// <summary>args → TSettings（T は引数なしコンストラクタ必須）</summary>
        public static T Parse<T>(string[] args) where T : new()
        {
            var schema = BuildSchema(typeof(T));
            var tokens = new Queue<string>(args);
            
            var instance = new T();
            
            while (tokens.Count > 0)
            {
                var token = tokens.Dequeue();
                
                // オプションでない場合：エラー扱い（シンプル実装）
                if (!token.StartsWith('-'))
                    throw new ArgumentException($"Unexpected token: {token}");
                
                // フラグ/オプション一致検索
                if (!schema.TryGetValue(token, out var meta))
                    throw new ArgumentException($"Unknown option: {token}");
                
                var prop = meta.Prop;
                object? value;
                
                if (meta.IsFlag && prop.PropertyType == typeof(bool))
                {
                    value = true; // スイッチが現れた=有効
                }
                else
                {
                    if (tokens.Count == 0)
                        throw new ArgumentException($"Missing value for option {token}");
                    var raw = tokens.Dequeue();
                    value = ConvertSimple(raw, prop.PropertyType);
                }
                prop.SetValue(instance, value);
            }
            return instance;
        }
        
        /// <summary>TSettings → string[]</summary>
        public static string[] Serialize<T>(T settings)
        {
            var type = typeof(T);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var output = new List<string>();
            
            foreach (var prop in props)
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;
                
                var current = prop.GetValue(settings);
                var defaultVal = prop.PropertyType.IsValueType
                    ? Activator.CreateInstance(prop.PropertyType)
                    : null;
                
                // 既定値と同じならスキップ（bool flag の false など）
                if (Equals(current, defaultVal)) continue;
                
                // 既定値比較をもう少し厳密に行いたい場合はここを拡張
                var primaryName = attr.Names.First();
                
                if (attr.IsFlag && prop.PropertyType == typeof(bool) && Equals(current, true))
                {
                    output.Add(primaryName);
                }
                else
                {
                    output.Add(primaryName);
                    output.Add(current?.ToString() ?? string.Empty);
                }
            }
            return output.ToArray();
        }
        
        
        private static Dictionary<string, (PropertyInfo Prop, bool IsFlag)> BuildSchema(Type t)
        {
            var dict = new Dictionary<string, (PropertyInfo, bool)>(StringComparer.Ordinal);
            
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var opt = p.GetCustomAttribute<OptionAttribute>();
                if (opt == null) continue;
                
                foreach (var name in opt.Names)
                {
                    dict[name] = (p, opt.IsFlag);
                }
            }
            return dict;
        }
        
        private static object? ConvertSimple(string raw, Type target)
        {
            if (target == typeof(string)) return raw;
            if (target == typeof(int)) return int.Parse(raw);
            if (target == typeof(bool)) return bool.Parse(raw);
            if (target.IsEnum) return Enum.Parse(target, raw, ignoreCase: true);
            // 必要に応じて型を追加
            return Convert.ChangeType(raw, target);
        }
    }
}