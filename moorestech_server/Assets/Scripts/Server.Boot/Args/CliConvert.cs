#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Server.Boot.Args;
using UnityEngine;

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
                    
                    // クォートされている値をアンクォート
                    raw = UnquoteValue(raw);
                    
                    value = ConvertSimple(raw, prop.PropertyType);
                }
                prop.SetValue(instance, value);
            }
            return instance;
        }
        
        /// <summary>TSettings → string[]</summary>
        public static string[] Serialize<T>(T settings) where T : new()
        {
            var type = typeof(T);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var output = new List<string>();
            
            // デフォルトインスタンスを作成してプロパティの初期値を取得
            var defaultInstance = new T();
            
            foreach (var prop in props)
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;
                
                var current = prop.GetValue(settings);
                var defaultVal = prop.GetValue(defaultInstance);
                
                // 既定値と同じならスキップ
                if (Equals(current, defaultVal)) continue;
                
                var primaryName = attr.Names.First();
                
                if (attr.IsFlag && prop.PropertyType == typeof(bool))
                {
                    // フラグオプションは true の場合のみ出力
                    if (Equals(current, true))
                    {
                        output.Add(primaryName);
                    }
                }
                else
                {
                    output.Add(primaryName);
                    var value = current?.ToString() ?? string.Empty;
                    
                    // 値をクォート処理
                    value = QuoteValue(value);
                    
                    output.Add(value);
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
        
        /// <summary>値にスペースや特殊文字が含まれる場合、適切にクォート処理を行う</summary>
        private static string QuoteValue(string value)
        {
            // null または空文字列の場合はそのまま返す
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;
            
            // スペース、タブ、ダブルクォート、バックスラッシュのいずれかを含む場合はクォートが必要
            bool needsQuoting = value.Any(c => c == ' ' || c == '\t' || c == '"' || c == '\\');
            
            if (!needsQuoting)
                return value;
            
            // ダブルクォートで囲み、内部のダブルクォートとバックスラッシュをエスケープ
            var escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            
            return $"\"{escaped}\"";
        }
        
        /// <summary>クォートされた値をアンクォートする</summary>
        private static string UnquoteValue(string value)
        {
            // クォートされていない場合はそのまま返す
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return value;
            
            if (!value.StartsWith('"') || !value.EndsWith('"'))
                return value;
            
            // 前後のダブルクォートを削除
            var unquoted = value.Substring(1, value.Length - 2);
            
            // StringBuilderを使って効率的に文字列を構築
            var sb = new StringBuilder(unquoted.Length);
            
            // 文字列をスキャンしてエスケープシーケンスを処理
            for (int i = 0; i < unquoted.Length; i++)
            {
                // 現在の文字がバックスラッシュで、かつ文字列の末尾でない場合
                if (unquoted[i] == '\\' && i + 1 < unquoted.Length)
                {
                    char nextChar = unquoted[i + 1];
                    if (nextChar == '\\' || nextChar == '"')
                    {
                        // 次の文字（エスケープ対象）を追加
                        sb.Append(nextChar);
                        i++; // 次の文字を消費したのでインデックスを1つ進める
                    }
                    else
                    {
                        // エスケープシーケンスではない場合はそのまま追加
                        sb.Append(unquoted[i]);
                    }
                }
                else
                {
                    // 通常の文字はそのまま追加
                    sb.Append(unquoted[i]);
                }
            }
            
            return sb.ToString();
        }
    }
}