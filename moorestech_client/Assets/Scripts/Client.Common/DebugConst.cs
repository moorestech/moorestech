namespace Client.Game
{
    public static class DebugConst
    {
        public const string IsItemListViewForceShowLabel = "Item list view force show";
        public const string IsItemListViewForceShowKey = "IsItemListViewForceShow";

        public const string SkitPlaySettingsLabel = "Skit play setting";
        public const string SkitPlaySettingsKey = "SkitPlaySettings";

        public const string MapObjectSuperMineLabel = "Map object super mine";
        public const string MapObjectSuperMineKey = "MapObjectSuperMine";

        public const string FixCraftTimeLabel = "Fix fast craft time";
        public const string FixCraftTimeKey = "FixCraftTime";

        public const string TrainAutoRunLabel = "Train auto run";
        public const string TrainAutoRunKey = "TrainAutoRun";

        public const string PlacePreviewKeepLabel = "Place preview keep (no send)";
        public const string PlacePreviewKeepKey = "PlacePreviewKeep";

        public const string FpsLimitLabel = "FPS Limit";
        public const string FpsLimitKey = "FpsLimit";
    }

    /// <summary>
    /// FPS制限の選択肢
    /// FPS limit options
    /// </summary>
    public enum DebugFpsLimit
    {
        Fps10 = 10,
        Fps20 = 20,
        Fps30 = 30,
        Fps60 = 60,
        Fps120 = 120,
        Fps144 = 144,
    }
}