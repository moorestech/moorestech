using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Action;
using Mooresmaster.Model.GameActionModule;
using NUnit.Framework;

namespace Tests.UnitTest.Game.GameAction
{
    /// <summary>
    ///     全 GameActionType を「ロード時に再実行する」「一度きり」のちょうど一方へ分ける網羅テスト
    ///     Exhaustively sorts every GameActionType into exactly one of "replayed on load" or "run once"
    /// </summary>
    public class UnlockReplayGameActionTypesCoverageTest
    {
        // 一度きりで、ロード時・チャレンジ再開時に再実行しない種別（付与とスキット）
        // Kinds that run once and are never replayed on load or challenge resume (grants and skits)
        private static readonly string[] RunOnceGameActionTypes =
        {
            GameActionElement.GameActionTypeConst.giveItem,
            GameActionElement.GameActionTypeConst.playSkit,
            GameActionElement.GameActionTypeConst.playBackgroundSkit,
        };

        [Test]
        public void EveryGameActionTypeIsReplayedOrRunOnceTest()
        {
            // 種別が増えた日はここが赤くなり、再実行するか一度きりかの判断を強制する（switch にだけ足すとロード後にその解放が黙って消えるため）
            // A new kind turns this red and forces the replay-or-once decision (adding it only to the switch would silently lose that unlock after loading)
            var ledger = UnlockReplayGameActionTypes.Types.Concat(RunOnceGameActionTypes).ToList();
            CollectionAssert.AreEquivalent(DeclaredGameActionTypes(), ledger);
        }

        [Test]
        public void ReplayedAndRunOnceDoNotOverlapTest()
        {
            CollectionAssert.IsEmpty(UnlockReplayGameActionTypes.Types.Intersect(RunOnceGameActionTypes));
        }

        // 自動生成の GameActionTypeConst から宣言済みの種別を全部取り出す
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
