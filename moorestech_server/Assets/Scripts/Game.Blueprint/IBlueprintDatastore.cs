using System;
using System.Collections.Generic;

namespace Game.Blueprint
{
    public interface IBlueprintDatastore
    {
        IReadOnlyList<BlueprintJsonObject> Blueprints { get; }

        // 名前は加工せず登録し、発行したGuidを返す
        // Registers without renaming and returns the issued GUID
        Guid Register(BlueprintJsonObject blueprint);
        bool Delete(Guid blueprintGuid);
        bool TryGet(Guid blueprintGuid, out BlueprintJsonObject blueprint);

        List<BlueprintJsonObject> GetSaveJsonObject();
        void LoadBlueprints(List<BlueprintJsonObject> blueprints);
    }
}
