// 装備枠のクリック可否が grab 成立画面と一致することを固定する
// Pins that equipment-slot clickability matches exactly the screens where a grab holds
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PlayerInventoryData } from "@/bridge";

const host = vi.hoisted(() => ({
  uiState: null as { state: string } | null,
  inventory: null as PlayerInventoryData | null,
  dispatchAction: vi.fn(),
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.inventory ? host.inventory : null),
    useTopicSelector: (topic: string, selector: (data: unknown) => unknown) =>
      selector(topic === actual.Topics.uiState ? host.uiState : null),
    readTopic: (topic: string) => (topic === actual.Topics.inventory ? host.inventory : null),
    dispatchAction: host.dispatchAction,
  };
});

// window 依存のグローバル wheel だけ外し、grab 判定は本物のフックを通す
// Stub only the window-bound global wheel; the grab predicate still runs for real
vi.mock("@/shared/uiState", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/uiState")>()),
  useGameLayerWheel: () => {},
}));

// スロットは props だけ観測したいので、Mantine 依存を避けた印付きの素の要素へ置き換える
// Only the slot props matter here, so replace it with a marked bare element free of Mantine dependencies
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("span", { ...props, "data-mock": "item-slot" }),
}));

import EquipmentPanel from "./index";

const slot = (itemId: number, count: number) => ({ itemId, count });

function renderSlots() {
  const renderer = create(createElement(EquipmentPanel));
  return renderer.root.findAllByProps({ "data-mock": "item-slot" });
}

describe("EquipmentPanel のクリック受付", () => {
  beforeEach(() => {
    host.dispatchAction.mockReset();
    host.inventory = {
      mainSlots: [slot(0, 0)],
      hotbarSlots: [slot(0, 0)],
      grab: slot(0, 0),
      selectedHotbar: 0,
      equipment: [slot(1, 3)],
      selectedEquipment: -1,
    };
  });

  it("pauseMenu 中の左押下はハンドラごと無く、アクションも飛ばない", () => {
    host.uiState = { state: "PauseMenu" };

    const slots = renderSlots();

    expect(slots).toHaveLength(1);
    expect(slots[0].props.onLeftDown).toBeUndefined();
    expect(slots[0].props.onRightDown).toBeUndefined();
    expect(slots[0].props.onDoubleClick).toBeUndefined();
    expect(host.dispatchAction).not.toHaveBeenCalled();
  });

  it("GameScreen 中も同様にクリックを受けない", () => {
    host.uiState = { state: "GameScreen" };

    expect(renderSlots()[0].props.onLeftDown).toBeUndefined();
  });

  it("持ち物画面では左押下が掴み取りを送る", () => {
    host.uiState = { state: "PlayerInventory" };

    const target = renderSlots()[0];
    act(() => target.props.onLeftDown(false));

    expect(host.dispatchAction).toHaveBeenCalledWith("inventory.move_item", {
      from: { area: "equipment", slot: 0 },
      to: { area: "grab", slot: 0 },
      count: 3,
    });
  });
});
