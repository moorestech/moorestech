using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BuildMenuModule;

namespace Server.Protocol.PacketResponse.Util.ConnectTool
{
    /// <summary>
    /// 解放済みconnectToolをToolType別にSortPriority昇順で選ぶサーバー側ヘルパー
    /// Server-side helper selecting unlocked connectTools by ToolType in ascending SortPriority
    /// </summary>
    public static class ConnectToolSelector
    {
        // 指定connectToolが解放済みか
        // Whether the given connectTool is unlocked
        public static bool IsUnlocked(System.Guid connectToolGuid)
        {
            var infos = ServerContext.GetService<IGameUnlockStateDataController>().ConnectToolUnlockStateInfos;
            return infos.TryGetValue(connectToolGuid, out var info) && info.IsUnlocked;
        }

        public static IEnumerable<ConnectToolMasterElement> UnlockedByToolType(string toolType)
        {
            // グローバル解放状態を参照し、指定ToolTypeの解放済みエントリのみを昇順で返す
            // Reference global unlock state; return only unlocked entries of the given ToolType, ascending
            var infos = ServerContext.GetService<IGameUnlockStateDataController>().ConnectToolUnlockStateInfos;
            return MasterHolder.ConnectToolMaster.All
                .Where(element => element.ToolType == toolType)
                .Where(element => infos.TryGetValue(element.ConnectToolGuid, out var info) && info.IsUnlocked)
                .OrderBy(element => element.SortPriority);
        }
    }
}
