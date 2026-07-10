using System.Collections.Generic;
using System.Linq;

namespace Game.Blueprint
{
    public class BlueprintDatastore : IBlueprintDatastore
    {
        private readonly List<BlueprintJsonObject> _blueprints = new();

        public IReadOnlyList<BlueprintJsonObject> Blueprints => _blueprints;

        public string Register(BlueprintJsonObject blueprint)
        {
            // 重複名には " (2)" 形式の連番を付与して常に登録成功させる
            // Suffix duplicates with " (2)" style numbering so register always succeeds
            var name = blueprint.Name;
            var suffix = 2;
            while (_blueprints.Any(b => b.Name == name))
            {
                name = $"{blueprint.Name} ({suffix})";
                suffix++;
            }

            blueprint.Name = name;
            _blueprints.Add(blueprint);
            return name;
        }

        public bool Delete(string name)
        {
            var target = _blueprints.FirstOrDefault(b => b.Name == name);
            if (target == null) return false;

            _blueprints.Remove(target);
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
