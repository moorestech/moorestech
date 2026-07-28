using System;
using System.Collections.Generic;

namespace Game.PlacementTarget
{
    // ブループリントの供給元だけサーバ/クライアントで差し替える
    // Only the blueprint source differs between server and client
    public interface IBlueprintCatalogSource
    {
        IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; }
    }
}
