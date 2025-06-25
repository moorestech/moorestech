using System.Linq;
using Mooresmaster.Loader.CharactersModule;
using Mooresmaster.Model.CharactersModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CharacterMaster
    {
        public readonly Characters Characters;
        public CharacterMasterElement[] ChallengeMasterElements => Characters.Data;
        
        public CharacterMaster(JToken itemJToken)
        {
            Characters = CharactersLoader.Load(itemJToken);
        }
        
        public CharacterMasterElement GetCharacterMaster(string id)
        {
            return Characters.Data.FirstOrDefault(x => x.CharacterId == id);
        }
    }
}