using System;
using System.Collections.Generic;
using Game.Blueprint;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     設置対象の解決に必要なBP一覧と本体の参照窓口
    ///     Read access to blueprint entries and bodies for placement target resolution
    /// </summary>
    public interface IBlueprintLookup
    {
        IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; }
        bool TryGetBlueprint(Guid blueprintGuid, out BlueprintJsonObject blueprint);
    }
}
