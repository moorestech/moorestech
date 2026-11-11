using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// 参照キャプチャ 3-build-menu の中身（mock-host の buildMenuFixtures と日本語の名前）を写した VM。並べ比べの撮影だけが使う
    /// View model copying the 3-build-menu reference content (mock-host buildMenuFixtures and Japanese names); used only by the comparison capture
    /// </summary>
    public static class BuildMenuReferenceDemo
    {
        private static Guid G(string text) => Guid.Parse(text);

        public static BuildMenuViewModel Create()
        {
            // カテゴリとサブカテゴリ（fixtures の categories の順。建材は空のカテゴリ）
            // Categories and sub-categories in fixture order; 建材 is the empty category
            var layout = new (string category, string name, (string sub, string subName)[] subs)[]
            {
                ("51000000-0000-4000-8000-000000000001", "物流", new[] { ("52000000-0000-4000-8000-000000000001", "チェスト"), ("52000000-0000-4000-8000-000000000002", "電気コンベア") }),
                ("51000000-0000-4000-8000-000000000002", "輸送", new[] { ("52000000-0000-4000-8000-000000000003", "鉄道"), ("52000000-0000-4000-8000-000000000004", "車両") }),
                ("51000000-0000-4000-8000-000000000003", "ブループリント", new[] { ("52000000-0000-4000-8000-000000000005", "保存済み") }),
                ("51000000-0000-4000-8000-000000000004", "建材", new[] { ("52000000-0000-4000-8000-000000000006", "土台") }),
                ("51000000-0000-4000-8000-000000000005", "採掘", new[] { ("52000000-0000-4000-8000-000000000007", "採掘機") }),
                ("51000000-0000-4000-8000-000000000006", "生産", new[] { ("52000000-0000-4000-8000-000000000008", "原始加工") }),
                ("51000000-0000-4000-8000-000000000007", "動力", new[] { ("52000000-0000-4000-8000-000000000009", "シャフト") }),
                ("51000000-0000-4000-8000-000000000008", "電力", new[] { ("52000000-0000-4000-8000-000000000010", "発電") }),
                ("51000000-0000-4000-8000-000000000009", "液体", new[] { ("52000000-0000-4000-8000-000000000011", "パイプ") }),
                ("51000000-0000-4000-8000-000000000010", "ツール", new[] { ("52000000-0000-4000-8000-000000000012", "接続") }),
                ("51000000-0000-4000-8000-000000000011", "建築マテリアル", new[] { ("52000000-0000-4000-8000-000000000013", "内装パネル") }),
            };
            var categories = layout.Select(c => new BuildMenuCategory(G(c.category), c.subs.Select(sub => G(sub.sub)).ToList())).ToList();
            var names = new Dictionary<Guid, UiText>();
            foreach (var c in layout)
            {
                names[G(c.category)] = UiText.FromVerbatim(c.name);
                foreach (var sub in c.subs) names[G(sub.sub)] = UiText.FromVerbatim(sub.subName);
            }

            // エントリ（fixtures の entries の順。量産 80 件は輸送/車両、追加 7 カテゴリは 1 件ずつ）
            // Entries in fixture order: 80 fillers under 輸送/車両 and one entry per extra category
            var entries = new List<BuildMenuEntryViewModel>
            {
                Entry("53000000-0000-4000-8000-000000000001", PlacementTargetKind.Block, "01", "01", "木のチェスト", Rows(4, 4), false, BuildMenuSetPlacementNone.Instance, Seed(1)),
                Entry("53000000-0000-4000-8000-000000000002", PlacementTargetKind.Block, "01", "01", "鉄のチェスト", Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(2)),
                Entry("53000000-0000-4000-8000-000000000003", PlacementTargetKind.Block, "01", "02", "ベルトコンベア", Rows(1, 1), true, new BuildMenuSetPlacementAvailable(3, 2), Seed(3)),
                Entry("53000000-0000-4000-8000-000000000004", PlacementTargetKind.Block, "02", "03", "鉄道レール", Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(4)),
                Entry("56000000-0000-4000-8000-000000000001", PlacementTargetKind.TrainCar, "02", "04", "貨物車両", Rows(), false, BuildMenuSetPlacementNone.Instance, new PlacementTargetTrainCarIcon(G("56000000-0000-4000-8000-000000000001"))),
                Entry("55000000-0000-4000-8000-000000000001", PlacementTargetKind.ConnectTool, "02", "03", "電線接続ツール", Rows(), false, BuildMenuSetPlacementNone.Instance, new PlacementTargetConnectToolIcon(G("55000000-0000-4000-8000-000000000001"))),
                Entry("58000000-0000-4000-8000-000000000001", PlacementTargetKind.BlueprintCopy, "03", "05", "ブループリントコピー", Rows(), false, BuildMenuSetPlacementNone.Instance, PlacementTargetNameOnlyIcon.Instance),
                Entry("3f6a9c1e-8b2d-4f7a-9e3c-1a2b3c4d5e6f", PlacementTargetKind.Blueprint, "03", "05", "starter-base", Rows(), false, BuildMenuSetPlacementNone.Instance, PlacementTargetNameOnlyIcon.Instance),
            };
            for (var i = 0; i < 80; i++) entries.Add(Entry($"53000000-0000-4000-8000-0000000010{i:D2}", PlacementTargetKind.Block, "02", "04", FillerLabel(i), Rows(), false, BuildMenuSetPlacementNone.Instance, Seed(8 + i % 12)));
            var extras = new[] { ("05", "07", "05", "電動採掘機"), ("06", "08", "06", "石の加工台"), ("07", "09", "07", "動力シャフト"), ("08", "10", "08", "石炭発電機"), ("09", "11", "09", "銅パイプ"), ("10", "12", "10", "電線接続具"), ("11", "13", "11", "装飾パネル") };
            foreach (var (category, sub, entry, label) in extras) entries.Add(Entry($"53000000-0000-4000-8000-0000000000{entry}", PlacementTargetKind.Block, category, sub, label, Rows(2, 2), false, BuildMenuSetPlacementNone.Instance, Seed(1)));
            return new BuildMenuViewModel(categories, names, entries);
        }

        // 量産エントリは mock-host の辞書に名前が無いので id を置く。マスは絵だけを描き、参照の状態では名前は出ない
        // Fillers have no name in the mock-host dictionary, so the id stands in; slots draw only pictures, so the reference state never shows it
        private static string FillerLabel(int index) => $"53000000-0000-4000-8000-0000000010{index:D2}";

        private static BuildMenuEntryViewModel Entry(string id, PlacementTargetKind kind, string category, string sub, string label, IReadOnlyList<BuildMenuMaterialRow> rows, bool paymentWaived, BuildMenuSetPlacement setPlacement, PlacementTargetIcon icon)
        {
            return new BuildMenuEntryViewModel(G(id), kind, G($"51000000-0000-4000-8000-0000000000{category}"), G($"52000000-0000-4000-8000-0000000000{sub}"), label, rows, paymentWaived, icon, setPlacement);
        }

        // fixture の blockIconUrl(n) の n。絵は BuildMenuReferenceDemoTextures が種 n の PNG を返す
        // The n of fixture blockIconUrl(n); BuildMenuReferenceDemoTextures returns the seed-n PNG for it
        private static PlacementTargetIcon Seed(int n) => new PlacementTargetBlockIcon(new BlockId(n));

        // 素材は fixtures の itemId 1（count・held）。詳細はヒントのままなので見た目には出ない
        // Materials use fixture itemId 1 (count, held); the detail stays on the hint, so they never show
        private static IReadOnlyList<BuildMenuMaterialRow> Rows() => Array.Empty<BuildMenuMaterialRow>();

        private static IReadOnlyList<BuildMenuMaterialRow> Rows(int count, int held) => new[] { new BuildMenuMaterialRow(new ItemId(1), new BuildMenuRequiredItem(Guid.Empty, count, held, held < count)) };
    }
}
