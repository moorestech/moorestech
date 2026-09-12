using System;
using System.Collections.Generic;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport
{
    public readonly struct UnityLogEntry
    {
        public readonly DateTime Time;
        public readonly ulong Tick;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;

        public UnityLogEntry(DateTime time, ulong tick, LogType type, string message, string stackTrace)
        {
            Time = time;
            Tick = tick;
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    // 直近のUnityログを保持する。どのスレッドのログも受けるためロックで守る
    // Keeps the most recent Unity logs; locked because logs arrive from any thread
    public sealed class UnityLogRing : IInitializable
    {
        public const int Capacity = 2000;

        private readonly object _lock = new();
        private readonly Queue<UnityLogEntry> _entries = new(Capacity + 1);

        public void Initialize()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public void Add(LogType type, string message, string stackTrace)
        {
            var isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            var entry = new UnityLogEntry(DateTime.UtcNow, GameUpdater.CurrentTick, type, message, isError ? stackTrace : "");
            lock (_lock)
            {
                _entries.Enqueue(entry);
                if (_entries.Count > Capacity) _entries.Dequeue();
            }
        }

        public IReadOnlyList<UnityLogEntry> Dump()
        {
            lock (_lock)
            {
                return new List<UnityLogEntry>(_entries);
            }
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            Add(type, condition, stackTrace);
        }
    }
}
