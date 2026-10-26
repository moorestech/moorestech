using System;
using Client.Common.Server;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util.Network
{
    // 保存完了後にクライアント端点を閉じ、通常の送信周期を跨いで残フレームを観測する
    // Close the client endpoint after flushing and observe remaining frames across normal send intervals
    internal static class InitializedClientEndpointCleanup
    {
        internal static async UniTask DisconnectAndObserveAsync()
        {
            var startFrame = Time.frameCount;
            var startTime = Time.time;
            var realStart = Time.realtimeSinceStartup;
            ClientContext.VanillaApi.Disconnect();
            Debug.Log($"[ClientEndpointCleanup] disconnected utc:{DateTime.UtcNow:O} frame:{startFrame}");

            // 時間を進めたまま既存間隔の2周期を監視し、停止中の無限待機を避ける
            // Observe two existing intervals while time advances and reject a paused infinite wait
            Assert.Greater(Time.timeScale, 0f, "Residual client ticks require advancing game time");
            var observationSeconds = NetworkConst.UpdateIntervalSeconds * 2f;
            while (Time.time - startTime < observationSeconds) await UniTask.Yield();
            Debug.Log($"[ClientEndpointCleanup] observed utc:{DateTime.UtcNow:O} frames:{Time.frameCount - startFrame} gameSeconds:{Time.time - startTime} realSeconds:{Time.realtimeSinceStartup - realStart} interval:{NetworkConst.UpdateIntervalSeconds}");
        }
    }
}
