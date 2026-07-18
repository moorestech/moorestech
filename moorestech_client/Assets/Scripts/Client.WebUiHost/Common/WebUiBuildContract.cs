namespace Client.WebUiHost.Common
{
    /// <summary>
    /// C#ホストとWeb UI成果物が共有する配信契約
    /// Serving contract shared by the C# host and Web UI artifact
    /// </summary>
    public static class WebUiBuildContract
    {
        public const string ContractVersion = "webui-a3-v1";
        public const string ManifestFileName = "webui-manifest.json";
        public const string StreamingAssetsDirectory = "WebUi/dist";
    }
}
