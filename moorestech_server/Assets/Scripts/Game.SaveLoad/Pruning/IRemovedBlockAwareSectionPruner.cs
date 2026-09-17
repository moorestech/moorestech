using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>world節で除去したブロックの巻き添えまで取り除く節の除去器。world節の除去より後に走る</summary>
    /// <summary>A section pruner that also removes collateral of the blocks pruned from the world section; it runs after the world section is pruned</summary>
    public interface IRemovedBlockAwareSectionPruner
    {
        // セーブJSON直下のキー。WorldSaveAllInfoV1のJsonProperty名と一致させる
        // The top-level key in the save JSON, matching a JsonProperty name of WorldSaveAllInfoV1
        string SaveSectionName { get; }

        // 渡した節をその場で書き換え、取り除いた実体を返す
        // Rewrites the given section in place and returns the removed entities
        MissingMasterSectionPruneResult Prune(JToken section, RemovedWorldBlockPositions removedWorldBlockPositions);
    }
}
