using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>セーブ形式を1つ上の版へ変換する1手。worldVersionの更新は連鎖側が行う</summary>
    /// <summary>One hop that converts the save to the next version; the chain updates worldVersion itself</summary>
    public interface ISaveMigrationStep
    {
        int FromVersion { get; }

        JObject Migrate(JObject save);
    }
}
