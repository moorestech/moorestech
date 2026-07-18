using System;

namespace Client.WebUiHost.Static
{
    [Serializable]
    public class WebUiArtifactManifest
    {
        public string contractVersion;
        public string buildVersion;
        public WebUiArtifactFile[] files;
    }

    [Serializable]
    public class WebUiArtifactFile
    {
        public string path;
        public string sha256;
    }
}
