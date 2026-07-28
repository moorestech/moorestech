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
            // 名前は加工せずGuidを発行して登録する
            // Register without renaming; issue a GUID as the identity
            var guid = Guid.NewGuid();
            blueprint.SetBlueprintGuid(guid);
            _blueprints.Add(blueprint);
            return guid;
        }

        public bool Delete(Guid blueprintGuid)
        {
            var index = _blueprints.FindIndex(b => b.BlueprintGuid == blueprintGuid);
            if (index < 0) return false;
            _blueprints.RemoveAt(index);
            return true;
        }

        public bool TryGet(Guid blueprintGuid, out BlueprintJsonObject blueprint)
        {
            blueprint = _blueprints.FirstOrDefault(b => b.BlueprintGuid == blueprintGuid);
            return blueprint != null;
        }

        public List<BlueprintJsonObject> GetSaveJsonObject()
        {
            return new List<BlueprintJsonObject>(_blueprints);
        }

        public void LoadBlueprints(List<BlueprintJsonObject> blueprints)
        {
            _blueprints.Clear();
            _blueprints.AddRange(blueprints);

            // 旧セーブ（Guid未発行）を読み込み時に補完する。ユーザー生成データの欠損補完である
            // Backfill legacy saves lacking a GUID at load time; this is user-data completion
            foreach (var blueprint in _blueprints)
            {
                if (blueprint.BlueprintGuid == Guid.Empty) blueprint.SetBlueprintGuid(Guid.NewGuid());
            }
        }
    }
}
