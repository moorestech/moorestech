import { describe, it, expect } from "vitest";

import { parseTopicPayload } from "./validators";
import { loadFixture } from "./wireFixtures.test-helper";
import { BENIGN_ERRORS } from "../transport/actions";
import { TopicEnvelopeSchema, Topics } from "../transport/protocol";
import type { PlayerInventoryData, BlockInventoryData, ProgressData, ModalData, UiStateData, BuildMenuData, ChallengeTreeData, ChallengeCurrentData, PauseMenuData, NotificationData } from "./payloadTypes";

describe("wire contract fixtures (shared with C#)", () => {
  it("削除した重複採掘HUD topicと読み手のない削除モードtopicを公開しない", () => {
    expect(Object.values(Topics)).not.toContain("ui.mining_hud");
    expect(Object.values(Topics)).not.toContain("ui.delete_mode");
  });

  it("accepts Phase C4 presentation fixtures", () => {
    expect(parseTopicPayload(Topics.gameState, loadFixture("game_state.json")).valid).toBe(true);
    expect(parseTopicPayload(Topics.tutorialPresentation, loadFixture("tutorial_presentation.json")).valid).toBe(true);
    expect(parseTopicPayload(Topics.worldPins, loadFixture("world_pins.json")).valid).toBe(true);
    expect(parseTopicPayload(Topics.skitPresentation, loadFixture("skit_presentation.json")).valid).toBe(true);
  });
  it("topic envelope requires a non-negative revision", () => {
    const envelope = TopicEnvelopeSchema.parse(loadFixture("topic_envelope.json"));
    expect(envelope.revision).toBe(42);
    expect(parseTopicPayload(envelope.topic, envelope.data).valid).toBe(true);
  });
  it("inventory_snapshot が受理され型消費できる", () => {
    const data = loadFixture("inventory_snapshot.json");
    expect(parseTopicPayload(Topics.inventory, data).valid).toBe(true);
    const inv = data as PlayerInventoryData;
    expect(inv.mainSlots.length).toBe(3);
    expect(inv.grab.count).toBe(0);
    expect(inv.selectedEquipment).toBe(1);
    expect(inv.equipmentSelectionConfirmationRevision).toBe(7);
  });

  it("block_inventory は open(presence)/closed(omission) の両方が受理される", () => {
    const open = loadFixture("block_inventory_open.json");
    const closed = loadFixture("block_inventory_closed.json");
    expect(parseTopicPayload(Topics.blockInventory, open).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, closed).valid).toBe(true);

    const openData = open as BlockInventoryData;
    expect(openData.open).toBe(true);
    if (openData.open && openData.source === "block") {
      expect(openData.itemSlots.length).toBe(2);
      expect(openData.fluidSlots.length).toBe(1);
      expect(openData.progress).toBe(0.5);
    }

    const closedData = closed as BlockInventoryData;
    expect(closedData.open).toBe(false);
    // 閉状態は他フィールドが省略される
    // The closed state omits every other field
    expect("blockType" in closedData).toBe(false);
  });

  it("train.riding と貨車inventory fixtureを受理する", () => {
    expect(parseTopicPayload(Topics.trainRiding, loadFixture("train_riding.json")).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, loadFixture("train_inventory.json")).valid).toBe(true);
  });

  it("progress は label あり(presence)/なし(omission) の両方が受理される", () => {
    const withLabel = loadFixture("progress_with_label.json");
    const noLabel = loadFixture("progress_no_label.json");
    expect(parseTopicPayload(Topics.progress, withLabel).valid).toBe(true);
    expect(parseTopicPayload(Topics.progress, noLabel).valid).toBe(true);
    expect((withLabel as ProgressData).label).toBe("Crafting");
    expect((noLabel as ProgressData).label).toBeUndefined();
  });

  it("modal は open(presence)/none(omission) の両方が受理される", () => {
    const open = loadFixture("modal_open.json");
    const none = loadFixture("modal_none.json");
    expect(parseTopicPayload(Topics.modal, open).valid).toBe(true);
    expect(parseTopicPayload(Topics.modal, none).valid).toBe(true);
    expect((open as ModalData).modal?.id).toBe("m1");
    expect((none as ModalData).modal).toBeUndefined();
  });

  it("build_menu_snapshot が受理され型消費できる", () => {
    const d = loadFixture("build_menu_snapshot.json");
    expect(parseTopicPayload(Topics.buildMenu, d).valid).toBe(true);
    const typed = d as BuildMenuData;
    expect(typed.entries[0].kind).toBe("block");
    expect(typed.entries[0].id).toBe("30000000-0000-4000-8000-000000000001");
    expect(typed.entries[0].categoryGuid).toBe("10000000-0000-4000-8000-000000000001");
    expect(typed.categories[0].categoryGuid).toBe("10000000-0000-4000-8000-000000000001");
    expect(typed.entries[0].label).toBeUndefined();
    expect(typed.entries[3].iconUrl).toBeUndefined();
  });

  it("modal_input が受理され input フラグを型消費できる", () => {
    const d = loadFixture("modal_input.json");
    expect(parseTopicPayload(Topics.modal, d).valid).toBe(true);
    const typed = d as ModalData;
    expect(typed.modal?.input).toBe(true);
  });

  it("ui_state が受理され型消費できる", () => {
    const data = loadFixture("ui_state.json");
    expect(parseTopicPayload(Topics.uiState, data).valid).toBe(true);
    expect((data as UiStateData).state).toBe("PlayerInventory");
  });

  it("pause_menu が切断状態を受理する", () => {
    const data = loadFixture("pause_menu.json");
    expect(parseTopicPayload(Topics.pauseMenu, data).valid).toBe(true);
    expect((data as PauseMenuData).disconnected).toBe(true);
  });

  it("C2 HUD/common fixtures are accepted", () => {
    const cases = [
      [Topics.placementMode, "placement_mode.json"],
      [Topics.placementMode, "placement_mode_connect_tool.json"],
      [Topics.placementMode, "placement_mode_train_car.json"],
      [Topics.crosshair, "visibility.json"],
      [Topics.uiVisibility, "visibility.json"],
      [Topics.tooltip, "tooltip.json"],
    ] as const;
    for (const [topic, fixture] of cases) expect(parseTopicPayload(topic, loadFixture(fixture)).valid).toBe(true);
  });

  it("契約違反 payload はバリデータで破棄される", () => {
    expect(parseTopicPayload(Topics.inventory, { mainSlots: "nope" }).valid).toBe(false);
    expect(parseTopicPayload(Topics.progress, { visible: true }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { open: true }).valid).toBe(false);
    expect(parseTopicPayload(Topics.modal, { modal: { id: "x" } }).valid).toBe(false);
  });
});

describe("block detail fixtures", () => {
  const cases = [
    "block_inventory_machine.json",
    "block_inventory_gear_machine.json",
    "block_inventory_generator.json",
    "block_inventory_miner.json",
    "block_inventory_filter_splitter.json",
    "block_inventory_electric_to_gear.json",
    "block_inventory_train_platform.json",
    "block_inventory_train_fluid_platform.json",
    "block_inventory_electric_pole.json",
  ];
  for (const file of cases) {
    it(`accepts ${file} and types it as open`, () => {
      const data = loadFixture(file);
      expect(parseTopicPayload(Topics.blockInventory, data).valid).toBe(true);
      const payload = data as BlockInventoryData;
      if (!payload.open) throw new Error("fixture must be open");
      expect(payload.blockType.length).toBeGreaterThan(0);
    });
  }
  it("consumes capability fields with the declared types", () => {
    const machine = loadFixture("block_inventory_machine.json") as BlockInventoryData;
    if (!machine.open || machine.source !== "block" || !machine.machine) throw new Error("machine fixture shape");
    expect(machine.machine.slotLayout.input + machine.machine.slotLayout.output + machine.machine.slotLayout.module).toBe(machine.itemSlots.length);
    expect(machine.machine.selectedRecipeGuid).toBe("00000000-0000-0000-0000-000000000000");
    expect(machine.machine.blockGuid).toBe("11111111-1111-4111-8111-111111111111");
    const gear = loadFixture("block_inventory_gear_machine.json") as BlockInventoryData;
    if (!gear.open || gear.source !== "block" || !gear.machine || !gear.gearNetwork) throw new Error("gear fixture shape");
    expect(gear.machine.selectedRecipeGuid).toBe("00000000-0000-0000-0000-000000000000");
    expect(gear.machine.blockGuid).toBe("22222222-2222-4222-8222-222222222222");
    expect(["none", "rocked", "overRequirePower"]).toContain(gear.gearNetwork.stopReason);
  });
});

describe("challenge fixtures", () => {
  it("accepts tree and current payloads", () => {
    const tree = loadFixture("challenge_tree.json");
    const current = loadFixture("challenge_current.json");
    expect(parseTopicPayload(Topics.challengeTree, tree).valid).toBe(true);
    expect(parseTopicPayload(Topics.challengeCurrent, current).valid).toBe(true);
    expect((tree as ChallengeTreeData).categories[0].nodes[0].state).toBe("current");
    expect((current as ChallengeCurrentData).completedChallengeGuid).toBeUndefined();
  });
});

describe("notification fixture", () => {
  it("itemEarned payloadのcategory/messageId/countをC#側と一致させる", () => {
    const data = loadFixture("notification_item_earned.json");
    expect(parseTopicPayload(Topics.notification, data).valid).toBe(true);
    // countを持つのは獲得variantだけなので、その型で受けて読む
    // Only the earned variant carries a count, so the fixture is read through that variant
    const notification = data as Extract<NotificationData, { category: "itemEarned" }>;
    expect(notification.category).toBe("itemEarned");
    expect(notification.messageId).toBe("itemEarned.mined");
    expect(notification.itemId).toBe(5);
    expect(notification.count).toBe(8);
  });
});

describe("error codes shared source (error_codes.json)", () => {
  it("TS の良性エラーコードは共有 error_codes.json の部分集合", () => {
    const shared = new Set((loadFixture("error_codes.json") as { codes: string[] }).codes);
    for (const set of Object.values(BENIGN_ERRORS)) {
      for (const code of set ?? []) expect(shared.has(code)).toBe(true);
    }
  });
});
