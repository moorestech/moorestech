import { describe, it, expect } from "vitest";

import { validateTopicPayload } from "./validators";
import { loadFixture } from "./wireFixtures.test-helper";
import { BENIGN_ERRORS } from "../transport/actions";
import { TopicEnvelopeSchema, Topics } from "../transport/protocol";
import type { PlayerInventoryData, BlockInventoryData, ProgressData, ModalData, UiStateData, BuildMenuData, ChallengeTreeData, ChallengeCurrentData, PauseMenuData } from "./payloadTypes";

describe("wire contract fixtures (shared with C#)", () => {
  it("削除した重複採掘HUD topicと読み手のない削除モードtopicを公開しない", () => {
    expect(Object.values(Topics)).not.toContain("ui.mining_hud");
    expect(Object.values(Topics)).not.toContain("ui.delete_mode");
  });

  it("accepts Phase C4 presentation fixtures", () => {
    expect(validateTopicPayload(Topics.gameState, loadFixture("game_state.json"))).toBe(true);
    expect(validateTopicPayload(Topics.tutorialPresentation, loadFixture("tutorial_presentation.json"))).toBe(true);
    expect(validateTopicPayload(Topics.worldPins, loadFixture("world_pins.json"))).toBe(true);
    expect(validateTopicPayload(Topics.skitPresentation, loadFixture("skit_presentation.json"))).toBe(true);
  });
  it("topic envelope requires a non-negative revision", () => {
    const envelope = TopicEnvelopeSchema.parse(loadFixture("topic_envelope.json"));
    expect(envelope.revision).toBe(42);
    expect(validateTopicPayload(envelope.topic, envelope.data)).toBe(true);
  });
  it("inventory_snapshot が受理され型消費できる", () => {
    const data = loadFixture("inventory_snapshot.json");
    expect(validateTopicPayload(Topics.inventory, data)).toBe(true);
    const inv = data as PlayerInventoryData;
    expect(inv.mainSlots.length).toBe(3);
    expect(inv.grab.count).toBe(0);
    // 素手は負値の -1 が正準形。C#側と対称に、負値が型消費側まで素通しで届くことを固定する
    // Bare hands is canonically the negative -1; mirror the C# side by pinning that the negative value reaches the typed consumer untouched
    expect(inv.selectedEquipment).toBe(-1);
    expect(inv.equipmentSelectionConfirmationRevision).toBe(7);
  });

  it("block_inventory は open(presence)/closed(omission) の両方が受理される", () => {
    const open = loadFixture("block_inventory_open.json");
    const closed = loadFixture("block_inventory_closed.json");
    expect(validateTopicPayload(Topics.blockInventory, open)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, closed)).toBe(true);

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
    expect(validateTopicPayload(Topics.trainRiding, loadFixture("train_riding.json"))).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, loadFixture("train_inventory.json"))).toBe(true);
  });

  it("progress は label あり(presence)/なし(omission) の両方が受理される", () => {
    const withLabel = loadFixture("progress_with_label.json");
    const noLabel = loadFixture("progress_no_label.json");
    expect(validateTopicPayload(Topics.progress, withLabel)).toBe(true);
    expect(validateTopicPayload(Topics.progress, noLabel)).toBe(true);
    expect((withLabel as ProgressData).label).toBe("Crafting");
    expect((noLabel as ProgressData).label).toBeUndefined();
  });

  it("modal は open(presence)/none(omission) の両方が受理される", () => {
    const open = loadFixture("modal_open.json");
    const none = loadFixture("modal_none.json");
    expect(validateTopicPayload(Topics.modal, open)).toBe(true);
    expect(validateTopicPayload(Topics.modal, none)).toBe(true);
    expect((open as ModalData).modal?.id).toBe("m1");
    expect((none as ModalData).modal).toBeUndefined();
  });

  it("build_menu_snapshot が受理され型消費できる", () => {
    const d = loadFixture("build_menu_snapshot.json");
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(true);
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
    expect(validateTopicPayload(Topics.modal, d)).toBe(true);
    const typed = d as ModalData;
    expect(typed.modal?.input).toBe(true);
  });

  it("ui_state が受理され型消費できる", () => {
    const data = loadFixture("ui_state.json");
    expect(validateTopicPayload(Topics.uiState, data)).toBe(true);
    expect((data as UiStateData).state).toBe("PlayerInventory");
  });

  it("pause_menu が切断状態を受理する", () => {
    const data = loadFixture("pause_menu.json");
    expect(validateTopicPayload(Topics.pauseMenu, data)).toBe(true);
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
    for (const [topic, fixture] of cases) expect(validateTopicPayload(topic, loadFixture(fixture))).toBe(true);
  });

  it("契約違反 payload はバリデータで破棄される", () => {
    expect(validateTopicPayload(Topics.inventory, { mainSlots: "nope" })).toBe(false);
    expect(validateTopicPayload(Topics.progress, { visible: true })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { open: true })).toBe(false);
    expect(validateTopicPayload(Topics.modal, { modal: { id: "x" } })).toBe(false);
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
      expect(validateTopicPayload(Topics.blockInventory, data)).toBe(true);
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
    expect(validateTopicPayload(Topics.challengeTree, tree)).toBe(true);
    expect(validateTopicPayload(Topics.challengeCurrent, current)).toBe(true);
    expect((tree as ChallengeTreeData).categories[0].nodes[0].state).toBe("current");
    expect((current as ChallengeCurrentData).completedChallengeGuid).toBeUndefined();
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
