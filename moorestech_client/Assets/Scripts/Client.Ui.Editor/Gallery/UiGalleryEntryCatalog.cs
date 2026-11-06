using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Client.Ui.Editor.Gallery
{
    // 見本を TypeCache で集める。部品レーンは IUiGalleryEntry を 1 つ足すだけでよい
    // Collects entries through TypeCache, so a component lane only adds one IUiGalleryEntry
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
