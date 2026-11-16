using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 縦パネル→壁パネル置換用の版9時点guid
    /// Version-9 guids used to replace vertical panels with wall panels
    /// </summary>
    /// <remarks>
    /// マスタ側でこの GUID を変えないこと。ステップは凍結した値を直に持つ（出所 moorestech_master a00d3b62、#97）
    /// Never change these GUIDs on the master side; the step holds the frozen values directly (source: moorestech_master a00d3b62, #97)
    /// </remarks>
    internal static class CleanRoomPanelReplacementTable
    {
        public const string CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        public const string CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";

        // 版9時点のパネルの大きさ（マスタは後に5マス幅へ変わった）
        // Panel size as of version 9 (the master later widened it to five cells)
        public static readonly Vector3Int Version9PanelSize = Vector3Int.one;

        public static bool IsPanelGuid(string blockGuid)
        {
            return blockGuid == CleanRoomPanelGuid || blockGuid == CleanRoomWallPanelGuid;
        }
    }
}
