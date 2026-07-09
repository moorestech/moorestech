using System.Collections.Generic;

namespace Game.Blueprint
{
    public interface IBlueprintDatastore
    {
        IReadOnlyList<BlueprintJsonObject> Blueprints { get; }

        // 重複名は連番付与するため、確定した登録名を返す
        // Returns the final registered name after duplicate-suffixing
        string Register(BlueprintJsonObject blueprint);
        bool Delete(string name);

        List<BlueprintJsonObject> GetSaveJsonObject();
        void LoadBlueprints(List<BlueprintJsonObject> blueprints);
    }
}
