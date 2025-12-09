using Mooresmaster.Model.CharactersModule;

namespace Core.Master.Validator
{
    public static class CharacterMasterUtil
    {
        public static bool Validate(Characters characters, out string errorLogs)
        {
            // CharacterMasterは外部キー依存がないため、バリデーション成功を返す
            // CharacterMaster has no external key dependencies, so return success
            errorLogs = "";
            return true;
        }

        public static void Initialize(Characters characters)
        {
            // CharacterMasterは追加の初期化処理がないため、空実装
            // CharacterMaster has no additional initialization, so empty implementation
        }
    }
}
