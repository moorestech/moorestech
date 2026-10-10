using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Game.Blueprint;

namespace Client.Tests.PlaceSystem.Blueprint
{
    /// <summary>
    ///     ライブラリ内部の格納形式に依存しない設置対象テスト用BP参照
    ///     Blueprint lookup for placement tests independent of library storage details
    /// </summary>
    internal sealed class BlueprintLookupStub : IBlueprintLookup
    {
        private readonly IReadOnlyList<BlueprintJsonObject> _blueprints;

        public BlueprintLookupStub(params BlueprintJsonObject[] blueprints)
        {
            _blueprints = blueprints;
        }

        public IReadOnlyList<(Guid id, string name)> BlueprintEntries =>
            _blueprints.Select(blueprint => (blueprint.BlueprintGuid, blueprint.Name)).ToArray();

        public bool TryGetBlueprint(Guid blueprintGuid, out BlueprintJsonObject blueprint)
        {
            blueprint = _blueprints.FirstOrDefault(value => value.BlueprintGuid == blueprintGuid);
            return blueprint != null;
        }
    }
}
