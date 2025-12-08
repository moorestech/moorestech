using System.Linq;
using Mooresmaster.Loader.CharactersModule;
using Mooresmaster.Model.CharactersModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CharacterMaster : IMasterValidator
    {
        public readonly Characters Characters;
        public CharacterMasterElement[] ChallengeMasterElements => Characters.Data;

        public CharacterMaster(JToken itemJToken)
        {
            Characters = CharactersLoader.Load(itemJToken);
        }

        public bool Validate(out string errorLogs)
        {
            // CharacterMasterは外部キー依存がないため、バリデーション成功を返す
            // CharacterMaster has no external key dependencies, so return success
            errorLogs = "";
            return true;
        }

        public void Initialize()
        {
            // CharacterMasterは追加の初期化処理がないため、空実装
            // CharacterMaster has no additional initialization, so empty implementation
        }

        public CharacterMasterElement GetCharacterMaster(string id)
        {
            return Characters.Data.FirstOrDefault(x => x.CharacterId == id);
        }
    }
}