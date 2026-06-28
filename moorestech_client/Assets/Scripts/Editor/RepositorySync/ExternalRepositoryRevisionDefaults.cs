namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositoryRevisionDefaults
    {
        public const string RevisionFileName = ".moorestech-external-revisions.json";

        public static ExternalRepositoryRevisionFile CreateRevisionFile()
        {
            var file = new ExternalRepositoryRevisionFile
            {
                repositories = new[]
                {
                    new ExternalRepositoryRevisionEntry("moorestech_master", "../moorestech_master", ""),
                    new ExternalRepositoryRevisionEntry("moorestech_client_private", "moorestech_client/Assets/PersonalAssets/moorestech-client-private", ""),
                },
            };

            return file;
        }
    }
}
