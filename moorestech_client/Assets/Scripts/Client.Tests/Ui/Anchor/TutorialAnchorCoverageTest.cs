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
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// アンカー id ごとに UITK の印付けか未移植の表のどちらか一方にだけ載ることと、ポーズのアンカーが解けることを確かめる（L1・plan §4.5）
    /// Verifies each anchor id is either marked in UITK or listed as pending (never both, never neither) and that pause anchors resolve (L1, plan §4.5)
    /// </summary>
    public class TutorialAnchorCoverageTest : ScreenTestHost
    {
        [Test]
        public void 全てのアンカーIDは印付けか未移植の表のどちらか一方にだけ載る()
        {
            var marked = MarkedAnchorIds();
            var problems = new List<string>();
            foreach (var anchorId in AllAnchorIds())
            {
                // 印も表も無ければ枠が永久に出ない。両方にあれば表の消し忘れ
                // Neither means the outline never shows; both means a stale table row
                var isMarked = marked.Contains(anchorId);
                var isPending = TutorialAnchorCoveragePending.Owners.ContainsKey(anchorId);
                if (!isMarked && !isPending) problems.Add($"{anchorId}: UITK で印が無く、未移植の表にも無い");
                if (isMarked && isPending) problems.Add($"{anchorId}: 印が付いたので未移植の表から {TutorialAnchorCoveragePending.Owners[anchorId]} の行を消す");
            }
            CollectionAssert.IsEmpty(problems);

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
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            var view = Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft()));
            view.SetShown(true);
            simulate.FrameUpdate();
            AssertUnique(AnchorIds.PauseMenu, AnchorIds.PauseSave, AnchorIds.PauseBack, AnchorIds.PauseSettings, AnchorIds.PauseBugReport);

            model.Set(FakePauseMenuModel.Page(PauseMenuPage.Settings));
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
