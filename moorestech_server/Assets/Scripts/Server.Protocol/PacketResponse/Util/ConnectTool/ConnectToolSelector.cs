using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BuildMenuModule;

namespace Server.Protocol.PacketResponse.Util.ConnectTool
{
    /// <summary>
    /// ToolType別の候補をSortPriority順で返す
    /// Lists candidates by ToolType in SortPriority order
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

        /// <summary>
        /// 指定ToolTypeの候補をSortPriority昇順で返す。解放状態は外から受け取り、クライアントは自分の解放状態を渡して同じ規則を共有する
        /// （プレビューと実接続で規則がずれると、繋がらない線を描いたり逆に描き漏らしたりする）。ignoreUnlockは無料設置デバッグ専用で、解放フィルタだけを外す（ADR 0056）
        /// Lists candidates of the ToolType ascending by SortPriority, taking the unlock state from outside so the client shares it with its own state
        /// (a drifted rule would preview wires that never connect, or miss ones that do); ignoreUnlock is for the free-placement debug only and drops just the unlock filter (ADR 0056)
        /// </summary>
        public static IEnumerable<ConnectToolMasterElement> CandidatesByToolType(string toolType, IGameUnlockStateData unlockState, bool ignoreUnlock)
        {
            // OrderByは安定ソートなので同順位はマスタ順を保つ
            // OrderBy is stable, so ties keep master order
            var infos = unlockState.ConnectToolUnlockStateInfos;
            return MasterHolder.ConnectToolMaster.All
                .Where(element => element.ToolType == toolType)
                .Where(element => ignoreUnlock || (infos.TryGetValue(element.ConnectToolGuid, out var info) && info.IsUnlocked))
                .OrderBy(element => element.SortPriority);
        }
    }
}
