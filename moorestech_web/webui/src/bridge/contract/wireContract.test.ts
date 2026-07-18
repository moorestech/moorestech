import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { validateTopicPayload } from "./validators";
import { BENIGN_ERRORS } from "../transport/actions";
import { Topics } from "../transport/protocol";
import type { PlayerInventoryData, BlockInventoryData, ProgressData, ModalData, UiStateData, ResearchTreeData, BuildMenuData } from "./payloadTypes";

// C# NUnit(WireContractTest) と同一のフィクスチャを参照する単一ソース。TS 側は validators と型消費で契約を確認する
// Single source shared with the C# NUnit (WireContractTest); the TS side checks the contract via validators + type consumption
const fixturesDir = fileURLToPath(
  new URL("../../../../../moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/", import.meta.url),
);

function loadFixture(name: string): unknown {
  return JSON.parse(readFileSync(fixturesDir + name, "utf8"));
}

describe("wire contract fixtures (shared with C#)", () => {
  it("inventory_snapshot が受理され型消費できる", () => {
    const data = loadFixture("inventory_snapshot.json");
    expect(validateTopicPayload(Topics.inventory, data)).toBe(true);
    const inv = data as PlayerInventoryData;
    expect(inv.mainSlots.length).toBe(2);
    expect(inv.grab.count).toBe(0);
    expect(inv.selectedHotbar).toBe(2);
  });

  it("block_inventory は open(presence)/closed(omission) の両方が受理される", () => {
    const open = loadFixture("block_inventory_open.json");
    const closed = loadFixture("block_inventory_closed.json");
    expect(validateTopicPayload(Topics.blockInventory, open)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, closed)).toBe(true);

    const openData = open as BlockInventoryData;
    expect(openData.open).toBe(true);
    if (openData.open) {
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
    expect(typed.entries[0].entryType).toBe("block");
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
    if (!machine.open || !machine.machine) throw new Error("machine fixture shape");
    expect(machine.machine.slotLayout.input + machine.machine.slotLayout.output + machine.machine.slotLayout.module).toBe(machine.itemSlots.length);
    const gear = loadFixture("block_inventory_gear_machine.json") as BlockInventoryData;
    if (!gear.open || !gear.gearNetwork) throw new Error("gear fixture shape");
    expect(["none", "rocked", "overRequirePower"]).toContain(gear.gearNetwork.stopReason);
  });
});

describe("research_tree fixture", () => {
  it("accepts and types research payload", () => {
    const data = loadFixture("research_tree.json");
    expect(validateTopicPayload(Topics.researchTree, data)).toBe(true);
    const tree = data as ResearchTreeData;
    expect(tree.nodes.length).toBe(2);
    expect(tree.nodes[1].prevGuids).toContain(tree.nodes[0].guid);
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
