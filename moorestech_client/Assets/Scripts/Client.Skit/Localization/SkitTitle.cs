using System;

namespace Client.Skit.Localization
{
    public static class SkitTitle
    {
        public static string FromAssetName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || assetName.Contains("."))
            {
                throw new ArgumentException("Skit asset name must be an extensionless basename");
            }

            return assetName;
        }
    }
}
