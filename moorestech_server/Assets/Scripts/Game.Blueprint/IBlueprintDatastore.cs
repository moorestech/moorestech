using System;
using System.Collections.Generic;

namespace Game.Blueprint
{
    public interface IBlueprintDatastore
    {
        IReadOnlyList<BlueprintJsonObject> Blueprints { get; }

        // 削除されたBPのGuidを流す。BPを参照する側が死んだ参照を捨てるために購読する
        // Emits the deleted blueprint's guid so referencing systems can drop the dead reference
        IObservable<Guid> OnBlueprintDeleted { get; }

        // 名前と生成済みGuidを加工せず登録する
        // Registers without changing the name or the pre-generated GUID
        Guid Register(BlueprintJsonObject blueprint);
        bool Delete(Guid blueprintGuid);

        List<BlueprintJsonObject> GetSaveJsonObject();
        void LoadBlueprints(List<BlueprintJsonObject> blueprints);
    }
}
