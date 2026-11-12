namespace Game.SaveLoad.Migration.Steps.V10ToV11
{
    /// <summary>
    /// 版10時点で1マスだった床・壁パネルのguid（版11でマスタが5マス幅へ変わった）
    /// Guids of the floor/wall panels that were one cell in version 10 (the master widened them to five cells in version 11)
    /// </summary>
    /// <remarks>
    /// ステップは現行マスタを引かず凍結値を直に持つ
    /// The step holds frozen values directly instead of reading the current master
    /// </remarks>
    internal static class Version10CleanRoomPanelGuids
    {
        private const string CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        private const string CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";

        public static bool Contains(string blockGuid)
        {
            return blockGuid == CleanRoomPanelGuid || blockGuid == CleanRoomWallPanelGuid;
        }
    }
}
