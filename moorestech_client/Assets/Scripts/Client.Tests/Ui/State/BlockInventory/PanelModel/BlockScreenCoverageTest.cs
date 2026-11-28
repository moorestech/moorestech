using System.Linq;
using System.Reflection;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// None 以外の全ブロック画面IDが本文を引け、標準の本文のIDだけが欄の設定を持つ
    /// Every block screen ID except None resolves a body, and exactly the standard-body IDs have section settings
    /// </summary>
    public class BlockScreenCoverageTest
    {
        [Test]
        public void None以外の全ブロック画面IDが登録されている()
        {
            var screens = typeof(BlockMasterElement.BlockScreenConst).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (string)field.GetRawConstantValue())
                .Where(screen => screen != BlockMasterElement.BlockScreenConst.None)
                .ToArray();
            foreach (var screen in screens)
            {
                // 標準の本文なら欄の設定も引ける
                // A standard-body ID also resolves its section settings
                var components = BlockComponentRegistry.Resolve(screen);
                if (components.Body == BlockInventoryBodyKind.SectionStack)
                {
                    Assert.IsNotNull(SectionStackConfigTable.Resolve(screen), screen);
                }
                else
                {
                    Assert.Throws<System.InvalidOperationException>(() => SectionStackConfigTable.Resolve(screen), screen);
                }
            }
            Assert.Throws<System.InvalidOperationException>(() => BlockComponentRegistry.Resolve(BlockMasterElement.BlockScreenConst.None));
            Assert.Throws<System.InvalidOperationException>(() => SectionStackConfigTable.Resolve(BlockMasterElement.BlockScreenConst.None));
        }
    }
}
