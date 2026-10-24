using System;

namespace Tests.Module.TestMod
{
    public class ForUnitTestMachineRecipeId
    {
        public static readonly Guid UnlockedMachineRecipe = new("f79c7ee0-1ebe-4fcc-b1ec-5bcb296d9c1a");
        public static readonly Guid LockedMachineRecipe = new("38dfacce-1234-4612-8c7c-29112c12409a");

        // 清浄室工程（成膜・EUV・ダイシング）のテスト用レシピ
        // Test recipes for the clean-room process (deposition, EUV, dicing)
        public static readonly Guid CleanRoomDepositionRecipe = new("ddcacf5c-a34d-4cc9-95e9-0d0e9a0f44ba");
    }
}
