using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>セーブの1節からマスタに無い参照を取り除く。節の形とguidの綴りは実装だけが知る</summary>
    /// <summary>Removes references absent from the master out of one save section; only the implementation knows the section's shape and guid spellings</summary>
    public interface IMissingMasterSectionPruner
    {
        // セーブJSON直下のキー。WorldSaveAllInfoV1のJsonProperty名と一致させる
        // The top-level key in the save JSON, matching a JsonProperty name of WorldSaveAllInfoV1
        string SaveSectionName { get; }

        // 渡した節をその場で書き換え、取り除いた実体を返す
        // Rewrites the given section in place and returns the removed entities
        MissingMasterSectionPruneResult Prune(JToken section);
    }
}
