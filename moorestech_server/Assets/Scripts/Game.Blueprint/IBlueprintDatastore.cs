using System;
using System.Collections.Generic;

namespace Game.Blueprint
{
    public interface IBlueprintDatastore
    {
        IReadOnlyList<BlueprintJsonObject> Blueprints { get; }

        // 名前と生成済みGuidを加工せず登録する
        // Registers without changing the name or the pre-generated GUID
        Guid Register(BlueprintJsonObject blueprint);
        bool Delete(Guid blueprintGuid);

        List<BlueprintJsonObject> GetSaveJsonObject();
        void LoadBlueprints(List<BlueprintJsonObject> blueprints);
    }
}
