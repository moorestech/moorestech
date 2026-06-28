using System;

namespace Client.Editor.RepositorySync
{
    [Serializable]
    public class ExternalRepositoryRevisionEntry
    {
        public string key;
        public string relativePath;
        public string commitHash;

        public ExternalRepositoryRevisionEntry(string key, string relativePath, string commitHash)
        {
            this.key = key;
            this.relativePath = relativePath;
            this.commitHash = commitHash;
        }
    }
}
