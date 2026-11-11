using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Text;
using Core.Master;
using Game.PlacementTarget;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    // Web の mock fixture（10 カテゴリ・物流/輸送/BP）と同じ形の ViewModel
    // View model shaped like the Web mock fixture (10 categories, logistics/transport/blueprint)
    internal static class BuildMenuSamples
    {
        public static readonly Guid Logistics = Guid.Parse("a0000000-0000-4000-8000-000000000001");
        public static readonly Guid Transport = Guid.Parse("a0000000-0000-4000-8000-000000000002");
        public static readonly Guid Blueprint = Guid.Parse("a0000000-0000-4000-8000-000000000003");
        public static readonly Guid EmptyCategory = Guid.Parse("a0000000-0000-4000-8000-0000000000ff");
        public static readonly Guid Sub = Guid.Parse("b0000000-0000-4000-8000-000000000001");
        public static readonly Guid WoodChest = Guid.Parse("c0000000-0000-4000-8000-000000000001");
        public static readonly Guid IronChest = Guid.Parse("c0000000-0000-4000-8000-000000000002");
        public static readonly Guid BeltConveyor = Guid.Parse("c0000000-0000-4000-8000-000000000003");
        public static readonly Guid Rail = Guid.Parse("c0000000-0000-4000-8000-000000000004");
        public static readonly Guid CargoCar = Guid.Parse("c0000000-0000-4000-8000-000000000005");
        public static readonly Guid StarterBase = Guid.Parse("c0000000-0000-4000-8000-000000000006");
        public static readonly Guid BuildingMaterials = Guid.Parse("a0000000-0000-4000-8000-000000000004");

        // Web の contentLocalizationFixtures.ts（english）の英訳を逐語で写す。Web の建材 2 つ（空の 0004・中身ありの 0011）は空と BuildingMaterials に当てる
        // Verbatim english names from the Web contentLocalizationFixtures.ts; the Web's two Building Materials (empty 0004, filled 0011) map to EmptyCategory and BuildingMaterials
        private static readonly string[] CategoryNames = { "Logistics", "Transport", "Blueprint", "Building Materials", "Mining", "Production", "Power", "Electricity", "Liquids", "Tools", "Building Materials" };

        public static BuildMenuViewModel Create()
        {
            return Create(Array.Empty<BuildMenuMaterialRow>(), PlacementTargetNameOnlyIcon.Instance);
        }

        // 木のチェストとベルトコンベアにだけ素材を持たせる版（詳細の素材の題を見る画面テスト用。素材の名前はテスト mod が要る）
        // Variant where only the wood chest and belt conveyor carry materials (for screen tests that read the detail's materials title; item names need the test mod)
        public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials)
        {
            return Create(materials, PlacementTargetNameOnlyIcon.Instance);
        }

        // 木のチェストだけ絵付き（中央寄せ測定用）
        // Variant where only the wood chest may carry a picture (for the screen test measuring icon centering)
        public static BuildMenuViewModel Create(IReadOnlyList<BuildMenuMaterialRow> materials, PlacementTargetIcon woodChestIcon)
        {
            // 物流・輸送・BP の後に 7 カテゴリ（各 1 件）、最後に空のカテゴリ。id は決め打ちにし、作り直しても構造が同じになるようにする
            // Logistics, transport, blueprint, then 7 categories with one entry each, then an empty one; ids are fixed so a rebuild keeps the same structure
            var others = Enumerable.Range(4, 7).Select(i => Guid.Parse($"a0000000-0000-4000-8000-{i:D12}")).ToList();
            var categoryGuids = new[] { Logistics, Transport, Blueprint }.Concat(others).Append(EmptyCategory).ToList();
            var categories = categoryGuids.Select(guid => new BuildMenuCategory(guid, new[] { Sub })).ToList();
            var entries = new List<BuildMenuEntryViewModel>
            {
                new(WoodChest, PlacementTargetKind.Block, Logistics, Sub, "木のチェスト", materials, false, woodChestIcon, BuildMenuSetPlacementNone.Instance),
                Block(IronChest, Logistics, "鉄のチェスト", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance),
                Block(BeltConveyor, Logistics, "ベルトコンベア", materials, false, new BuildMenuSetPlacementAvailable(3, 2)),
                Block(Rail, Transport, "鉄道レール", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance),
                new(CargoCar, PlacementTargetKind.TrainCar, Transport, Sub, "貨車", Array.Empty<BuildMenuMaterialRow>(), false, PlacementTargetNameOnlyIcon.Instance, BuildMenuSetPlacementNone.Instance),
                new(StarterBase, PlacementTargetKind.Blueprint, Blueprint, Sub, "スターター拠点", Array.Empty<BuildMenuMaterialRow>(), false, PlacementTargetNameOnlyIcon.Instance, BuildMenuSetPlacementNone.Instance),
            };
            entries.AddRange(others.Select((guid, i) => Block(Guid.Parse($"c1000000-0000-4000-8000-{i:D12}"), guid, $"機械{i}", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance)));

            // 見出しは Web の english シナリオの英語名（最長は Building Materials）を逐語で入れる
            // Headings carry the Web english-scenario names verbatim (the longest is Building Materials)
            var names = new Dictionary<Guid, UiText> { [Sub] = UiText.FromVerbatim("General") };
            for (var i = 0; i < categoryGuids.Count; i++) names[categoryGuids[i]] = UiText.FromVerbatim(CategoryNames[i]);
            return new BuildMenuViewModel(categories, names, entries);
        }

        public static BuildMenuEntryViewModel Block(Guid id, Guid category, string label, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, BuildMenuSetPlacement setPlacement)
        {
            return new BuildMenuEntryViewModel(id, PlacementTargetKind.Block, category, Sub, label, materials, paymentWaived, PlacementTargetNameOnlyIcon.Instance, setPlacement);
        }

        // 本番と同じ絵の引き方（テクスチャは白、撮影は空）
        // Same picture lookup as production (white textures, no captures)
        public static PlacementTargetIcons Icons(GameContentPresentation content)
        {
            return new PlacementTargetIcons(content, new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture), new BlueprintThumbnailContainer());
        }

        public static BuildMenuMaterialRow Material(ItemId itemId, int count, int held)
        {
            return new BuildMenuMaterialRow(itemId, new BuildMenuRequiredItem(Guid.NewGuid(), count, held, held < count));
        }
    }
}
