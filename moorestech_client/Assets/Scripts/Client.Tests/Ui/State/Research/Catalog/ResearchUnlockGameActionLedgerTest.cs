using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Ui.State.Research.Catalog;
using Mooresmaster.Model.GameActionModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research.Catalog
{
    /// <summary>
    /// UITK の研究画面の表示/非表示台帳を GameActionType 全数と突き合わせる（前例 ResearchUnlockGameActionCoverageTest）
    /// Reconciles the UITK research screen's shown/hidden ledger with every GameActionType (precedent ResearchUnlockGameActionCoverageTest)
    /// </summary>
    public class ResearchUnlockGameActionLedgerTest
    {
        [Test]
        public void 全GameActionTypeが表示台帳か非表示台帳のどちらかに載っている()
        {
            // 種別が増えた日はここが赤くなり、表示するか出さないかの裁定を強制する
            // A newly added kind turns this red and forces the show-or-hide decision
            var ledger = ResearchUnlockSetFromMaster.DisplayedGameActionTypes.Concat(ResearchUnlockSetFromMaster.HiddenGameActionTypes).ToList();

            CollectionAssert.AreEquivalent(DeclaredGameActionTypes(), ledger);
        }

        [Test]
        public void 表示台帳と非表示台帳は重複しない()
        {
            CollectionAssert.IsEmpty(ResearchUnlockSetFromMaster.DisplayedGameActionTypes.Intersect(ResearchUnlockSetFromMaster.HiddenGameActionTypes));
        }

        // 自動生成の GameActionTypeConst から宣言済み種別を全数取り出す
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
