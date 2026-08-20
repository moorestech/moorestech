using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mooresmaster.Model.GameActionModule;
using NUnit.Framework;

namespace Client.Tests.WebUi.Research
{
    /// <summary>
    /// GameActionType 全数と研究画面の表示/非表示台帳の突き合わせ
    /// Reconciles every GameActionType against the research screen's shown/hidden ledger
    /// </summary>
    public class ResearchUnlockGameActionCoverageTest
    {
        // ResearchNodeDtoFactory が case を持つ表示6種（ADR 0014 決定3）
        // The 6 kinds ResearchNodeDtoFactory renders as cases (ADR 0014 decision 3)
        private static readonly string[] DisplayedGameActionTypes =
        {
            GameActionElement.GameActionTypeConst.giveItem,
            GameActionElement.GameActionTypeConst.unlockItemRecipeView,
            GameActionElement.GameActionTypeConst.unlockBlock,
            GameActionElement.GameActionTypeConst.unlockMachineRecipe,
            GameActionElement.GameActionTypeConst.unlockConnectTool,
            GameActionElement.GameActionTypeConst.unlockTrainCar,
        };

        // 解放はされるが研究画面には出さないと決めた種別
        // Kinds that are unlocked but deliberately not shown on the research screen
        private static readonly string[] HiddenGameActionTypes =
        {
            GameActionElement.GameActionTypeConst.unlockCraftRecipe,
            GameActionElement.GameActionTypeConst.unlockChallengeCategory,
            GameActionElement.GameActionTypeConst.unlockItemStackLevel,
            GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel,
            GameActionElement.GameActionTypeConst.unlockBlueprint,
            GameActionElement.GameActionTypeConst.playSkit,
            GameActionElement.GameActionTypeConst.playBackgroundSkit,
        };

        [Test]
        public void 全GameActionTypeが表示台帳か非表示台帳のどちらかに載っている()
        {
            // 種別が増えた日はここが赤くなり、表示するか出さないかの裁定を強制する
            // A newly added kind turns this red and forces the show-or-hide decision
            var declared = DeclaredGameActionTypes();
            var ledger = DisplayedGameActionTypes.Concat(HiddenGameActionTypes).ToList();

            CollectionAssert.AreEquivalent(declared, ledger);
        }

        [Test]
        public void 表示台帳と非表示台帳は重複しない()
        {
            CollectionAssert.IsEmpty(DisplayedGameActionTypes.Intersect(HiddenGameActionTypes));
        }

        // 自動生成のGameActionTypeConstから宣言済み種別を全数取り出す
        // Pull every declared kind out of the generated GameActionTypeConst
        private static List<string> DeclaredGameActionTypes()
        {
            return typeof(GameActionElement.GameActionTypeConst)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .ToList();
        }
    }
}
