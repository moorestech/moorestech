using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Spectre.Console.Cli;

namespace Server.Boot.Args
{
    public static class CliSerializer
    {
        public static string[] ToArgs<T>(T settings) where T : CommandSettings
        {
            var list = new List<string>();
            
            foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // --- Option ---
                var opt = p.GetCustomAttribute<CommandOptionAttribute>();
                if (opt != null)
                {
                    var value = p.GetValue(settings);
                    if (value == null) continue;                         // null は無視
                    if (value is bool b)
                    {
                        if (b) list.Add(opt.LongNames.First());          // true ならスイッチを出力
                    }
                    else
                    {
                        // 例: デフォルト値判定（任意）
                        var defaultVal = Activator.CreateInstance<T>()!
                            .GetType().GetProperty(p.Name)!.GetValue(Activator.CreateInstance<T>());
                        if (!Equals(value, defaultVal))
                        {
                            list.Add(opt.LongNames.First());
                            list.Add(value.ToString()!);
                        }
                    }
                }
                
                // --- Argument ---
                var arg = p.GetCustomAttribute<CommandArgumentAttribute>();
                if (arg != null)
                {
                    var value = p.GetValue(settings);
                    if (value != null) list.Add(value.ToString()!);
                }
            }
            return list.ToArray();
        }
    }
}