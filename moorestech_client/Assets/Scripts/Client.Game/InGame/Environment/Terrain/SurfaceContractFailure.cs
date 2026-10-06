using System;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain
{
    // 地表表示契約の違反を、起動失敗の前に開発者が読めるログへ残してから例外にする唯一の口
    // The single place that logs a surface presentation contract violation for developers before it fails startup
    public static class SurfaceContractFailure
    {
        public static InvalidOperationException Create(string reason)
        {
            Debug.LogError(reason);
            return new InvalidOperationException(reason);
        }
    }
}
