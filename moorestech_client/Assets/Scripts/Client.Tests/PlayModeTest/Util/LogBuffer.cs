using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tools.Logging
{
    /// <summary>
    /// Editor 専用: Console に溜まっている内容を反射で吸い出す。
    /// ※ Unity の内部 API を用いるため、バージョン差で壊れる可能性があります。
    /// </summary>
    public static class LogBuffer
    {
        public struct Entry
        {
            public LogType type;
            public string message;
            public string stackTrace;
        }

        /// <summary>
        /// Console の全ログを列挙（Editor 専用）。
        /// </summary>
        public static List<Entry> EnumerateEditorConsoleEntries() => EditorConsole.EnumerateConsoleEntries().ToList();

        /// <summary>
        /// 現在のログ数を取得（Editor 専用）。
        /// </summary>
        public static int GetCurrentLogCount() => EditorConsole.GetLogCount();
        
        /// <summary>
        /// 指定されたインデックス以降のログのみを列挙（Editor 専用）。
        /// </summary>
        public static List<Entry> EnumerateEditorConsoleEntriesFrom(int startIndex) => EditorConsole.EnumerateConsoleEntriesFrom(startIndex).ToList();
        
        
        // ----- 内部実装（UnityEditor.LogEntries の反射） -----
        private static class EditorConsole
        {
            static Type _tLogEntries;
            static MethodInfo _miStart, _miEnd, _miGetCount, _miGetEntryCount, _miGetLinesAndModeFromEntryInternal;
            static bool _ready;

            public static IEnumerable<Entry> EnumerateConsoleEntries()
            {
                if (!EnsureReflection())
                {
                    Debug.LogError("[LogBuffer] UnityEditor.LogEntries 反射の初期化に失敗しました（Unity バージョン差の可能性）。");
                    yield break;
                }

                _miStart.Invoke(null, null);
                int count = (int)_miGetCount.Invoke(null, null);

                for (int i = 0; i < count; i++)
                {
                    // Unity 6000 系: GetEntryCount と GetLinesAndModeFromEntryInternal を使用して全文とモードを取得
                    int numberOfLines = (int)_miGetEntryCount.Invoke(null, new object[] { i });

                    object maskObj = 0;
                    object textObj = string.Empty;
                    var args = new object[] { i, numberOfLines, maskObj, textObj };
                    _miGetLinesAndModeFromEntryInternal.Invoke(null, args);

                    int mask = (int)args[2];
                    string allLines = (string)args[3] ?? string.Empty;

                    // 1行目をメッセージ、それ以降をスタックトレースとして扱う
                    string[] lines = allLines.Split(new[] { '\n' }, StringSplitOptions.None);
                    string condition = lines.Length > 0 ? lines[0].TrimEnd('\r') : string.Empty;
                    string stack = lines.Length > 1 ? string.Join("\n", lines.Skip(1)) : string.Empty;

                    yield return new Entry
                    {
                        type = ModeToLogType(mask, condition),
                        message = condition,
                        stackTrace = stack,
                    };
                }

                _miEnd.Invoke(null, null);
            }

            public static int GetLogCount()
            {
                if (!EnsureReflection())
                {
                    Debug.LogError("[LogBuffer] UnityEditor.LogEntries 反射の初期化に失敗しました（Unity バージョン差の可能性）。");
                    return 0;
                }

                return (int)_miGetCount.Invoke(null, null);
            }

            public static IEnumerable<Entry> EnumerateConsoleEntriesFrom(int startIndex)
            {
                if (!EnsureReflection())
                {
                    Debug.LogError("[LogBuffer] UnityEditor.LogEntries 反射の初期化に失敗しました（Unity バージョン差の可能性）。");
                    yield break;
                }

                _miStart.Invoke(null, null);
                int count = (int)_miGetCount.Invoke(null, null);

                for (int i = startIndex; i < count; i++)
                {
                    // Unity 6000 系: GetEntryCount と GetLinesAndModeFromEntryInternal を使用して全文とモードを取得
                    int numberOfLines = (int)_miGetEntryCount.Invoke(null, new object[] { i });

                    object maskObj = 0;
                    object textObj = string.Empty;
                    var args = new object[] { i, numberOfLines, maskObj, textObj };
                    _miGetLinesAndModeFromEntryInternal.Invoke(null, args);

                    int mask = (int)args[2];
                    string allLines = (string)args[3] ?? string.Empty;

                    // 1行目をメッセージ、それ以降をスタックトレースとして扱う
                    string[] lines = allLines.Split(new[] { '\n' }, StringSplitOptions.None);
                    string condition = lines.Length > 0 ? lines[0].TrimEnd('\r') : string.Empty;
                    string stack = lines.Length > 1 ? string.Join("\n", lines.Skip(1)) : string.Empty;

                    yield return new Entry
                    {
                        type = ModeToLogType(mask, condition),
                        message = condition,
                        stackTrace = stack,
                    };
                }

                _miEnd.Invoke(null, null);
            }

            static bool EnsureReflection()
            {
                if (_ready) return true;

                var asm = typeof(Editor).Assembly;
                _tLogEntries = asm.GetType("UnityEditor.LogEntries");
                if (_tLogEntries == null) return false;

                BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                _miStart  = _tLogEntries.GetMethod("StartGettingEntries", S);
                _miEnd    = _tLogEntries.GetMethod("EndGettingEntries", S);
                _miGetCount = _tLogEntries.GetMethod("GetCount", S);

                // Unity 6000 系 API
                _miGetEntryCount = _tLogEntries.GetMethod("GetEntryCount", S);
                _miGetLinesAndModeFromEntryInternal = _tLogEntries.GetMethod("GetLinesAndModeFromEntryInternal", S);

                _ready = (_miStart != null && _miEnd != null && _miGetCount != null
                          && _miGetEntryCount != null && _miGetLinesAndModeFromEntryInternal != null);
                return _ready;
            }

            // 内部 mask ビットをざっくり LogType に落とす（必要なら調整可）
            static LogType ModeToLogType(int mask, string condition)
            {
                // Error/Exception 相当
                if ((mask & 1) != 0 || (mask & 32) != 0 ||
                    condition.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                    return LogType.Error;

                // Warning 相当
                if ((mask & 2) != 0 || (mask & 4) != 0 || (mask & 64) != 0)
                    return LogType.Warning;

                // Assert 相当（稀）
                if ((mask & 8) != 0)
                    return LogType.Assert;

                return LogType.Log;
            }
        }
    }
}
