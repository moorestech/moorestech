using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Client.Ui.Editor.Gallery
{
    // 見本をTypeCacheで集める
    // Collect entries through TypeCache
    public static class UiGalleryEntryCatalog
    {
        public static IReadOnlyList<IUiGalleryEntry> CreateAll()
        {
            return TypeCache.GetTypesDerivedFrom<IUiGalleryEntry>()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Select(type => (IUiGalleryEntry)Activator.CreateInstance(type))
                .OrderBy(entry => entry.Family)
                .ThenBy(entry => entry.ComponentName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
