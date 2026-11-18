using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 選択の持ち主は同じ選択の再設定を通知しない（L0）
    /// The selection owner does not notify when the same selection is set again (L0)
    /// </summary>
    public class RecipeItemSelectionTest
    {
        [Test]
        public void 同じアイテムを選び直しても未選択を重ねても通知しない()
        {
            var selection = new RecipeItemSelection();
            var count = 0;
            selection.Current.Skip(1).Subscribe(_ => count++);

            // 選ぶ・同じものを選ぶ・外す・また外す の 4 回で通知は 2 回
            // Select, reselect the same, clear, clear again: four calls, two notifications
            selection.Select(new ItemId(1));
            selection.Select(new ItemId(1));
            selection.Clear();
            selection.Clear();

            Assert.AreEqual(2, count);
        }
    }
}
