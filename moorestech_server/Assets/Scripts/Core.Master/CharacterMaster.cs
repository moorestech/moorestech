using System.Linq;
using Core.Master.Validator;
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
            return CharacterMasterUtil.Validate(Characters, out errorLogs);
        }

        public void Initialize()
        {
            CharacterMasterUtil.Initialize(Characters);
        }

        public CharacterMasterElement GetCharacterMaster(string id)
        {
            return Characters.Data.FirstOrDefault(x => x.CharacterId == id);
        }
    }
}