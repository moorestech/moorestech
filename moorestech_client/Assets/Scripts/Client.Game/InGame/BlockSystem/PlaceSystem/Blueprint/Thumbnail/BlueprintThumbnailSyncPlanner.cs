using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    public readonly struct BlueprintThumbnailSyncPlan
    {
        public readonly IReadOnlyList<Guid> ToRender;
        public readonly IReadOnlyList<Guid> ToRemove;

        public BlueprintThumbnailSyncPlan(IReadOnlyList<Guid> toRender, IReadOnlyList<Guid> toRemove)
        {
            ToRender = toRender;
            ToRemove = toRemove;
        }
    }

    /// <summary>
    ///     ライブラリと撮影済みキャッシュの差分を計算する
    ///     Calculates the library and photographed-cache difference
    /// </summary>
    public static class BlueprintThumbnailSyncPlanner
    {
        public static BlueprintThumbnailSyncPlan Plan(IReadOnlyList<Guid> libraryGuids, IReadOnlyCollection<Guid> cachedGuids)
        {
            var library = new HashSet<Guid>(libraryGuids);
            var cached = new HashSet<Guid>(cachedGuids);
            var toRender = new List<Guid>();
            foreach (var guid in libraryGuids)
            {
                if (!cached.Contains(guid)) toRender.Add(guid);
            }

            var toRemove = new List<Guid>();
            foreach (var guid in cachedGuids)
            {
                if (!library.Contains(guid)) toRemove.Add(guid);
            }

            return new BlueprintThumbnailSyncPlan(toRender, toRemove);
        }
    }
}
