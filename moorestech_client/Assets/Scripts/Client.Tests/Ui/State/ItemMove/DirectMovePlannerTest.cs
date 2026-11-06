using Client.Ui.State.ItemMove;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.ItemMove.ItemMoveFixtures;

namespace Client.Tests.Ui.State.ItemMove
{
    public class DirectMovePlannerTest
    {
        private static readonly ItemId Item1 = new(1);

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">同種スタックの空きへ順に詰める（複数スタック配分）</web-test>
        [Test]
        public void 同種スタックの空きへ順に詰める()
        {
            var moves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(1, 98), Slot(2, 5), Slot(0, 0), Slot(1, 10) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(0, 2), new PlannedMove(3, 5) }, moves);
        }

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">スタックで吸収しきれない残りは最初の空スロットへ</web-test>
        [Test]
        public void スタックで吸収しきれない残りは最初の空スロットへ置く()
        {
            var moves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(1, 98), Slot(0, 0) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(0, 2), new PlannedMove(1, 5) }, moves);
        }

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">同種スタックに全量入るなら空スロットは使わない</web-test>
        [Test]
        public void 同種スタックに全量入るなら空スロットは使わない()
        {
            var moves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(1, 10), Slot(0, 0) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(0, 7) }, moves);
        }

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">同種が無ければ最初の空スロットへ全量</web-test>
        [Test]
        public void 同種が無ければ最初の空スロットへ全量置く()
        {
            var moves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(2, 3), Slot(0, 0), Slot(0, 0) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(1, 7) }, moves);
        }

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">満杯スタックは飛ばす</web-test>
        [Test]
        public void 満杯スタックは飛ばす()
        {
            var moves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(1, 100), Slot(0, 0) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(1, 7) }, moves);
        }

        /// <web-test file="src/shared/itemMove/planDirectMoves.test.ts">移動先が無ければ空配列、空スロットが無ければ部分配分のみ返す</web-test>
        [Test]
        public void 移動先が無ければ空で空スロットが無ければ部分配分だけ返す()
        {
            CollectionAssert.IsEmpty(DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(2, 5) }));
            var partialMoves = DirectMovePlanner.Plan(7, Item1, 100, new[] { Slot(1, 98), Slot(2, 5) });
            CollectionAssert.AreEqual(new[] { new PlannedMove(0, 2) }, partialMoves);
        }
    }
}
