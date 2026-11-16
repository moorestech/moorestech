using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// アンカー id ごとに UITK の印が付いていることと、ポーズのアンカーが解けることを確かめる（L1・plan §4.5）
    /// Verifies every anchor id is marked in UITK and that pause anchors resolve (L1, plan §4.5)
    /// </summary>
    public class TutorialAnchorCoverageTest : ScreenTestHost
    {
        [Test]
        public void 全てのアンカーIDはUITKで印が付いている()
        {
            // 印の無いアンカーは UITK で枠が永久に出ない。未移植の表は段階 5 で空になり消した
            // An unmarked anchor never shows its outline in UITK; the pending table emptied in stage 5 and was removed
            var marked = MarkedAnchorIds();
            var unmarked = AllAnchorIds().Where(anchorId => !marked.Contains(anchorId)).ToList();
            CollectionAssert.IsEmpty(unmarked, "UITK で TutorialAnchor.Mark の無いアンカー");

            #region Internal

            IEnumerable<string> AllAnchorIds()
            {
                // 固定 id と、接頭辞で作る id の接頭辞（AnchorIds の *Prefix 定数）
                // Static ids plus the prefixes of generated ids (AnchorIds *Prefix constants)
                var prefixes = typeof(AnchorIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral && field.Name.EndsWith("Prefix")).Select(field => (string)field.GetRawConstantValue());
                return AnchorIds.StaticIds.Concat(prefixes);
            }

            HashSet<string> MarkedAnchorIds()
            {
                // TutorialAnchor.Mark を呼ぶ Client.Ui のファイルが参照する AnchorIds の名を拾い、id に写す（工場メソッドは名前＋Prefix の定数）
                // Collect AnchorIds names referenced by Client.Ui files that call TutorialAnchor.Mark and map them to ids (factory methods map to Name+Prefix)
                var ids = new HashSet<string>();
                var root = Path.Combine(Application.dataPath, "Scripts", "Client.Ui");
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    if (!source.Contains("TutorialAnchor.Mark(")) continue;
                    foreach (Match match in Regex.Matches(source, @"AnchorIds\.(\w+)"))
                    {
                        var field = typeof(AnchorIds).GetField(match.Groups[1].Value) ?? typeof(AnchorIds).GetField(match.Groups[1].Value + "Prefix");
                        if (field != null && field.IsLiteral) ids.Add((string)field.GetRawConstantValue());
                    }
                }
                return ids;
            }

            #endregion
        }

        [Test]
        public void ポーズの6つのアンカーはポーズを載せると一意に解ける()
        {
            // トップでは枠と 4 ボタン、子画面を開くと「トップへ戻る」が木に入る
            // The top page carries the frame and four buttons; opening a sub-page adds Back-to-top
            var view = Mount(new PauseMenuView(new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
            view.SetShown(true);
            view.Redraw(FakePauseMenuModel.Page(PauseMenuPage.Top), FakeSettingsModel.Default());
            simulate.FrameUpdate();
            AssertUnique(AnchorIds.PauseMenu, AnchorIds.PauseSave, AnchorIds.PauseBack, AnchorIds.PauseSettings, AnchorIds.PauseBugReport);

            view.RenderPauseMenu(FakePauseMenuModel.Page(PauseMenuPage.Settings));
            simulate.FrameUpdate();
            AssertUnique(AnchorIds.PauseMenu, AnchorIds.PauseBackToTop);

            #region Internal

            void AssertUnique(params string[] anchorIds)
            {
                foreach (var anchorId in anchorIds)
                {
                    Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(TutorialAnchorRegistry.Resolve(rootVisualElement, anchorId), anchorId);
                }
            }

            #endregion
        }
    }
}
