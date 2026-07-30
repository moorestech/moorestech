using System;
using System.Collections.Generic;
using Mooresmaster.Model.CharactersModule;

namespace Core.Master.Validator
{
    public static class CharacterMasterUtil
    {
        public static bool Validate(Characters characters, out string errorLogs)
        {
            // CharacterGuid空値・重複拒否
            // Reject empty or duplicate CharacterGuids
            errorLogs = string.Empty;
            var assignedGuids = new HashSet<Guid>();
            foreach (var character in characters.Data)
            {
                if (character.CharacterGuid == Guid.Empty || !assignedGuids.Add(character.CharacterGuid))
                    errorLogs += $"[CharacterMaster] invalid or duplicate CharacterGuid:{character.CharacterGuid}\n";
            }

            return errorLogs == string.Empty;
        }

        public static void Initialize(Characters characters)
        {
            // CharacterMasterは追加の初期化処理がないため、空実装
            // CharacterMaster has no additional initialization, so empty implementation
        }
    }
}
