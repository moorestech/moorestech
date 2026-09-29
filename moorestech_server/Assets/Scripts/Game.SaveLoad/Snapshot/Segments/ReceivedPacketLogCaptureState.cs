using System;
using System.Threading;
using UnityEngine;

namespace Game.SaveLoad.Snapshot.Segments
{
    // 記録の稼働状態と欠損理由を、tickスレッドと取得側の間で公開する
    // Share capture state and degradation reason between tick and capture threads
    internal sealed class ReceivedPacketLogCaptureState
    {
        private int _isActive;
        private string _degradeReason = string.Empty;
        private long _degradedAtTick;

        internal bool IsActive => Volatile.Read(ref _isActive) != 0;
        internal string DegradeReason => Volatile.Read(ref _degradeReason);
        internal ulong DegradedAtTick => (ulong)Volatile.Read(ref _degradedAtTick);

        internal void Start()
        {
            Volatile.Write(ref _isActive, 1);
        }

        internal void Stop()
        {
            Volatile.Write(ref _isActive, 0);
        }

        internal void Degrade(string reason, ulong tick, Exception exception)
        {
            Stop();
            // 最初の理由を残し、後続の失敗で記録停止の原因を隠さない
            // Keep the first reason so later errors do not hide why capture stopped
            if (Volatile.Read(ref _degradeReason).Length == 0)
            {
                Volatile.Write(ref _degradedAtTick, (long)tick);
                Volatile.Write(ref _degradeReason, reason);
            }
            Debug.LogError($"{reason} 以後パケットログの記録を停止します tick:{tick} message:{exception.Message}");
        }
    }
}
