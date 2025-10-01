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
    /// Editor 専用: Console に溜まっている内容を反射で吸い出し、TSV でエクスポートする。
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
        
        
        // ----- 内部実装（UnityEditor.LogEntries の反射） -----
        private static class EditorConsole
        {
            static Type _tLogEntries;
            static Type _tLogEntry;
            static MethodInfo _miStart, _miEnd, _miGetCount, _miGetEntryInternal;
            static FieldInfo _fiConditionOrMessage, _fiStackTrace, _fiMode;
            static bool _ready;

            public static IEnumerable<Entry> EnumerateConsoleEntries()
            {
                if (!EnsureReflection())
                {
                    Debug.LogError("[LogBuffer] UnityEditor.LogEntries 反射の初期化に失敗しました（Unity バージョン差の可能性）。");
                    yield break;
                }

                object logEntryObj = Activator.CreateInstance(_tLogEntry);
                _miStart.Invoke(null, null);
                int count = (int)_miGetCount.Invoke(null, null);

                for (int i = 0; i < count; i++)
                {
                    _miGetEntryInternal.Invoke(null, new object[] { i, logEntryObj });

                    string condition = (_fiConditionOrMessage.GetValue(logEntryObj) as string) ?? string.Empty;
                    string stack     = (_fiStackTrace.GetValue(logEntryObj) as string) ?? string.Empty;
                    int mode         = _fiMode != null ? (int)_fiMode.GetValue(logEntryObj) : 0;

                    yield return new Entry
                    {
                        type = ModeToLogType(mode, condition),
                        message = condition,
                        stackTrace = stack,
                    };
                }
                _miEnd.Invoke(null, null);
            }

            static bool EnsureReflection()
            {
                if (_ready) return true;
                
                UnityEditor.LogEntries

                var asm = typeof(Editor).Assembly;
                _tLogEntries = asm.GetType("UnityEditor.LogEntries");
                _tLogEntry   = asm.GetType("UnityEditor.LogEntry");
                if (_tLogEntries == null || _tLogEntry == null) return false;

                BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _miStart            = _tLogEntries.GetMethod("StartGettingEntries", S);
                _miEnd              = _tLogEntries.GetMethod("EndGettingEntries", S);
                _miGetCount         = _tLogEntries.GetMethod("GetCount", S);
                _miGetEntryInternal = _tLogEntries.GetMethod("GetEntryInternal", S);

                // フィールド名はバージョン差あり（condition/message, stacktrace/stackTrace）
                _fiConditionOrMessage = _tLogEntry.GetField("condition", I)
                                        ?? _tLogEntry.GetField("message", I);
                _fiStackTrace         = _tLogEntry.GetField("stacktrace", I)
                                        ?? _tLogEntry.GetField("stackTrace", I);
                _fiMode               = _tLogEntry.GetField("mode", I);

                _ready = (_miStart != null && _miEnd != null && _miGetCount != null && _miGetEntryInternal != null
                          && _fiConditionOrMessage != null && _fiStackTrace != null && _fiMode != null);
                return _ready;
            }

            // 内部 mode ビットをざっくり LogType に落とす（必要なら調整可）
            static LogType ModeToLogType(int mode, string condition)
            {
                // Error/Exception 相当
                if ((mode & 1) != 0 || (mode & 32) != 0 ||
                    condition.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                    return LogType.Error;

                // Warning 相当
                if ((mode & 2) != 0 || (mode & 4) != 0 || (mode & 64) != 0)
                    return LogType.Warning;

                // Assert 相当（稀）
                if ((mode & 8) != 0)
                    return LogType.Assert;

                return LogType.Log;
            }
        }
    }
}
