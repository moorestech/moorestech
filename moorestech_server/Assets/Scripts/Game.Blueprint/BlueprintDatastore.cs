using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace Game.Blueprint
{
    public class BlueprintDatastore : IBlueprintDatastore
    {
        private readonly List<BlueprintJsonObject> _blueprints = new();
        private readonly Subject<Guid> _onBlueprintDeleted = new();

        public IReadOnlyList<BlueprintJsonObject> Blueprints => _blueprints;
        public IObservable<Guid> OnBlueprintDeleted => _onBlueprintDeleted;

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
            _onBlueprintDeleted.OnNext(blueprintGuid);
            return true;
        }

        public List<BlueprintJsonObject> GetSaveJsonObject()
        {
            return new List<BlueprintJsonObject>(_blueprints);
        }

        public void LoadBlueprints(List<BlueprintJsonObject> blueprints)
        {
            _blueprints.Clear();
            foreach (var blueprint in blueprints)
            {
                RemoveMalformedLines(blueprint.Wires, blueprint.BlueprintGuidStr);
                RemoveMalformedLines(blueprint.Chains, blueprint.BlueprintGuidStr);
                _blueprints.Add(blueprint);
            }
        }

        private static void RemoveMalformedLines(List<BlueprintLineJsonObject> lines, string blueprintGuid)
        {
            // セーブ境界で不正な線を除き理由を残す
            // Reject malformed saved connections at the save boundary with a reason
            for (var index = lines.Count - 1; index >= 0; index--)
            {
                if (Guid.TryParse(lines[index].ConnectToolGuidStr, out var toolGuid) && toolGuid != Guid.Empty) continue;
                Debug.LogWarning($"[BlueprintLoad] line skipped: malformed connectToolGuid blueprint={blueprintGuid} index={index}");
                lines.RemoveAt(index);
            }
        }
    }
}
