using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Blueprint
{
    public class BlueprintDatastore : IBlueprintDatastore
    {
        private readonly List<BlueprintJsonObject> _blueprints = new();

        public IReadOnlyList<BlueprintJsonObject> Blueprints => _blueprints;

        public Guid Register(BlueprintJsonObject blueprint)
        {
            // 識別子は生成時に確定済みのため、登録では加工しない
            // The identity is fixed at creation time, so registration does not mutate it
            _blueprints.Add(blueprint);
            return blueprint.BlueprintGuid;
        }

        public bool Delete(Guid blueprintGuid)
        {
            var index = _blueprints.FindIndex(b => b.BlueprintGuid == blueprintGuid);
            if (index < 0) return false;
            _blueprints.RemoveAt(index);
            return true;
        }

        public List<BlueprintJsonObject> GetSaveJsonObject()
        {
            return new List<BlueprintJsonObject>(_blueprints);
        }

        public void LoadBlueprints(List<BlueprintJsonObject> blueprints)
        {
            _blueprints.Clear();
            _blueprints.AddRange(blueprints);
        }
    }
}
