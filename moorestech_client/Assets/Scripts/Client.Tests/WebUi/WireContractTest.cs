using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// C#⇔TS のワイヤ契約テスト: 実 DTO を WebUiJson でシリアライズし正準フィクスチャと一致検証する
    /// フィクスチャは TS 側 vitest と同一ファイルを参照する単一ソース
    /// C#⇔TS wire-contract test: serialize real DTOs via WebUiJson and match them against canonical fixtures
    /// The fixtures are the single source, referenced by the TS-side vitest too
    /// </summary>
    public class WireContractTest
    {
        // 共通envelopeがrevisionとpayloadをフィクスチャ通り保持する
        // The common envelope preserves revision and payload exactly as declared by the fixture
        [Test]
        public void TopicEnvelopeMatchesFixture()
        {
            var data = WebUiJson.Serialize(new ProgressDto { Visible = true, Progress = 0.5f, Label = null });
            var actual = JToken.Parse(WebSocketEnvelope.BuildEnvelope("event", "ui.progress", 42, data));
            var expected = JToken.Parse(LoadFixture("topic_envelope.json"));
            Assert.IsTrue(JToken.DeepEquals(expected, actual));
        }

        // インベントリ snapshot は全フィールド必須（省略なし）の代表ケース
        // The inventory snapshot represents the all-fields-present (no omission) case
        [Test]
        public void InventorySnapshotMatchesFixture()
        {
            var dto = new PlayerInventoryDto
            {
                MainSlots = new List<SlotDto> { new SlotDto { ItemId = 1, Count = 10 }, new SlotDto { ItemId = 2, Count = 5 } },
                HotbarSlots = new List<SlotDto> { new SlotDto { ItemId = 3, Count = 1 } },
                Grab = new SlotDto { ItemId = 0, Count = 0 },
                SelectedHotbar = 2,
            };
            AssertMatchesFixture(dto, "inventory_snapshot.json");
        }

        // 開状態: blockType/itemSlots/fluidSlots/progress が全て存在する（presence 側）
        // Open state: blockType/itemSlots/fluidSlots/progress are all present (the presence variant)
        [Test]
        public void BlockInventoryOpenMatchesFixture()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = "Chest",
                Identifier = "block:1",
                BlockName = "Chest",
                ItemSlots = new List<BlockItemSlotDto> { new BlockItemSlotDto { ItemId = 1, Count = 7 }, new BlockItemSlotDto { ItemId = 2, Count = 4 } },
                FluidSlots = new List<BlockFluidSlotDto> { new BlockFluidSlotDto { FluidId = 10, Amount = 500, Capacity = 1000, Name = "Water" } },
                Progress = 0.5,
            };
            AssertMatchesFixture(dto, "block_inventory_open.json");
        }

        // 閉状態: NullValueHandling.Ignore で open 以外の全キーが省略される（omission 側）
        // Closed state: NullValueHandling.Ignore omits every key except open (the omission variant)
        [Test]
        public void BlockInventoryClosedMatchesFixture()
        {
            AssertMatchesFixture(new BlockInventoryDto { Open = false }, "block_inventory_closed.json");
        }

        // label あり: progress の label キーが存在する（presence 側）
        // With label: the progress label key is present (the presence variant)
        [Test]
        public void ProgressWithLabelMatchesFixture()
        {
            AssertMatchesFixture(new ProgressDto { Visible = true, Progress = 0.4f, Label = "Crafting" }, "progress_with_label.json");
        }

        // label なし: null の label キーが省略される（omission 側）
        // Without label: the null label key is omitted (the omission variant)
        [Test]
        public void ProgressNoLabelMatchesFixture()
        {
            AssertMatchesFixture(new ProgressDto { Visible = false, Progress = 0f, Label = null }, "progress_no_label.json");
        }

        // modal あり: modal オブジェクトが存在する（presence 側）
        // With modal: the modal object is present (the presence variant)
        [Test]
        public void ModalOpenMatchesFixture()
        {
            var dto = new ModalTopicDto
            {
                Modal = new ModalDto { Id = "m1", Title = "確認", Message = "これは確認ダイアログです", ButtonText = "OK", Variant = "confirm" },
            };
            AssertMatchesFixture(dto, "modal_open.json");
        }

        // 入力モーダル: input:true が配信される（BP名入力等）
        // Input modal: input:true is delivered (e.g. blueprint naming)
        [Test]
        public void ModalInputMatchesFixture()
        {
            var dto = new ModalTopicDto
            {
                Modal = new ModalDto { Id = "m2", Title = "ブループリント名", Message = "保存するブループリントの名前を入力してください", ButtonText = "保存", Variant = "confirm", Input = true },
            };
            AssertMatchesFixture(dto, "modal_input.json");
        }

        // modal なし: null の modal キーが省略され {} になる（omission 側）
        // Without modal: the null modal key is omitted, yielding {} (the omission variant)
        [Test]
        public void ModalNoneMatchesFixture()
        {
            AssertMatchesFixture(new ModalTopicDto { Modal = null }, "modal_none.json");
        }

        // 全 Action ハンドラ + dispatcher が返し得るエラーコードを error_codes.json が過不足なく網羅する
        // error_codes.json must exactly cover every error code the Action handlers + dispatcher can return
        [Test]
        public void ErrorCodesFixtureCoversAllHandlerCodes()
        {
            // 全 Actions/*.cs の ActionResult.Fail(...) と dispatcher の literal を grep して手維持する正準セット
            // The canonical set, hand-maintained by grepping ActionResult.Fail(...) across Actions/*.cs and dispatcher literals
            var expected = new HashSet<string>
            {
                "unknown_action", "host_stopping", "internal_error", "unknown_error",
                "invalid_payload", "invalid_count", "invalid_slot",
                "empty_slot", "insufficient_count", "grab_not_empty",
                "invalid_index", "invalid_recipe", "recipe_locked",
                "invalid_id", "invalid_result", "no_pending_modal",
                "invalid_state", "unsupported_state",
                "invalid_guid", "research_failed", "block_not_open",
                "invalid_direction", "filter_request_failed", "unknown_entry",
                "stale_session", "stale_revision", "intent_not_allowed", "unknown_choice",
            };

            var shared = JObject.Parse(LoadFixture("error_codes.json"))["codes"].ToObject<List<string>>();
            Assert.AreEqual(shared.Count, new HashSet<string>(shared).Count, "error_codes.json に重複コードがある / duplicate codes");
            Assert.That(new HashSet<string>(shared), Is.EquivalentTo(expected), "error_codes.json が C# のエラーコード集合と不一致 / mismatch with the C# error-code set");
        }

        // ui_state: 列挙名文字列1フィールドの最小契約（INFRA-6）
        // ui_state: the minimal one-field enum-name contract (INFRA-6)
        [Test]
        public void UiStateMatchesFixture()
        {
            AssertMatchesFixture(new UiStateDto { State = "PlayerInventory" }, "ui_state.json");
        }

        // ポーズメニューは切断表示に必要な状態だけを配信する
        // The pause menu sends only the state required for disconnect presentation
        [Test]
        public void PauseMenuMatchesFixture()
        {
            AssertMatchesFixture(new PauseMenuDto { Disconnected = true }, "pause_menu.json");
        }

        // ビルドメニュー: 全エントリ種別とアイコンURL省略の正準形
        // Build menu: the canonical form covering every entry type and icon-url omission
        [Test]
        public void BuildMenuMatchesFixture()
        {
            var dto = new BuildMenuTopicDto
            {
                Entries = new List<BuildMenuEntryDto>
                {
                    new() { EntryType = "block", EntryKey = "1", Label = "鉄の機械", Tooltip = "鉄の機械\n鉄インゴット x5", IconUrl = "/api/block-icons/1.png" },
                    new() { EntryType = "trainCar", EntryKey = "11111111-2222-3333-4444-555555555555", Label = "貨物車", Tooltip = "貨物車", IconUrl = "/api/train-car-icons/11111111-2222-3333-4444-555555555555.png" },
                    new() { EntryType = "connectTool", EntryKey = "BeltConveyor", Label = "ベルトコンベア", Tooltip = "ベルトコンベア", IconUrl = "/api/icons/3.png" },
                    new() { EntryType = "blueprintCopy", EntryKey = "", Label = "ブループリントコピー", Tooltip = "ブループリントコピー" },
                    new() { EntryType = "blueprint", EntryKey = "家", Label = "家", Tooltip = "家" },
                },
            };
            AssertMatchesFixture(dto, "build_menu_snapshot.json");
        }

        // DTO を WebUiJson でシリアライズし、キー順序差を無視して JToken.DeepEquals で照合する
        // Serialize the DTO via WebUiJson and match with JToken.DeepEquals, ignoring key-order differences
        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var expected = JToken.Parse(LoadFixture(fixtureName));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"{fixtureName} mismatch\nexpected: {expected}\nactual:   {actual}");
        }

        private static string LoadFixture(string fixtureName)
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            return File.ReadAllText(path);
        }
    }
}
