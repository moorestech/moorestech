// 装備枠のクリック可否が grab 成立画面と一致することを固定する
// Pins that equipment-slot clickability matches exactly the screens where a grab holds
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PlacementModeData, PlayerInventoryData } from "@/bridge";

const host = vi.hoisted(() => ({
  uiState: null as { state: string } | null,
  inventory: null as PlayerInventoryData | null,
  placementMode: null as PlacementModeData | null,
  dispatchAction: vi.fn(),
  wheelHandler: null as ((event: WheelEvent) => void) | null,
  connectionStatus: "open" as "connecting" | "restoring" | "open" | "reconnecting",
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.inventory ? host.inventory : null),
    // 購読フックだけが placementMode を配る。readTopic 経由へ戻すと抑止テストが落ちる
    // Only the subscribing hook serves placementMode; going back through readTopic makes the suppression tests fail
    useTopicSelector: (topic: string, selector: (data: unknown) => unknown) => {
      if (topic === actual.Topics.uiState) return selector(host.uiState);
      if (topic === actual.Topics.placementMode) return selector(host.placementMode);
      return selector(null);
    },
    readTopic: (topic: string) => (topic === actual.Topics.inventory ? host.inventory : null),
    dispatchAction: host.dispatchAction,
    useConnectionStatus: () => host.connectionStatus,
  };
});

// window 依存のグローバル wheel だけ外し、grab 判定は本物のフックを通す
// Stub only the window-bound global wheel; the grab predicate still runs for real
vi.mock("@/shared/uiState", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/uiState")>()),
  useGameLayerWheel: (handler: (event: WheelEvent) => void) => {
    host.wheelHandler = handler;
  },
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

// アンカーはコンポーネント要素と実DOM要素の双方に載るため、実DOM要素側だけを数える
// The anchor prop appears on both the component element and the host element, so count only the host elements
function renderAnchors() {
  const renderer = create(createElement(EquipmentPanel));
  return renderer.root
    .findAll((node) => typeof node.type === "string" && typeof node.props["data-tutorial-anchor"] === "string")
    .map((node) => node.props["data-tutorial-anchor"] as string);
}

describe("EquipmentPanel のクリック受付", () => {
  beforeEach(() => {
    host.dispatchAction.mockReset();
    host.dispatchAction.mockResolvedValue(true);
    host.wheelHandler = null;
    host.connectionStatus = "open";
    host.placementMode = null;
    host.inventory = {
      mainSlots: [slot(0, 0)],
      grab: slot(0, 0),
      equipment: [slot(1, 3)],
      selectedEquipment: 0,
      equipmentSelectionConfirmationRevision: 0,
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

  // 選択中の枠だけ選択アンカーを併せて名乗る
  // Only the selected slot also declares the selection anchor
  it("装備枠がアンカーを名乗り、選択中の枠には選択アンカーも付く", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory = {
      mainSlots: [slot(0, 0)],
      grab: slot(0, 0),
      equipment: [slot(0, 0), slot(0, 0)],
      selectedEquipment: 1,
      equipmentSelectionConfirmationRevision: 0,
    };

    expect(renderAnchors()).toEqual(["equipment.hud", "equipment.slot-0", "equipment.slot-1 equipment.selected-slot"]);
  });

  // 1枠では切り替え先が無いのでホイールは未装備へ落とさず、要求も送らない
  // With a single slot there is nothing to switch to, so the wheel neither drops to unequipped nor sends a request
  it("装備枠が1つのときはホイールで select_equipment を送らない", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1)];
    renderSlots();
    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
      host.wheelHandler!({ deltaY: -100, deltaMode: 0, target: null } as WheelEvent);
    });

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

  it("topic反映前の高速2ノッチはpending値から1→2へ進む", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();
    const wheel = host.wheelHandler;
    expect(wheel).not.toBeNull();

    // topic据置きで2入力を先行
    // Run two inputs ahead of the unchanged topic
    act(() => {
      wheel!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
      wheel!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
    expect(host.dispatchAction).toHaveBeenNthCalledWith(2, "inventory.select_equipment", { index: 2 });
  });

  it("楽観topicと古いechoが来ても未確認の末尾から次ノッチを計算する", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    let renderer: ReturnType<typeof create>;
    act(() => {
      renderer = create(createElement(EquipmentPanel));
    });
    const wheel = host.wheelHandler!;

    // 1→2先行後、topic値2だけ更新
    // After 1→2, update only the topic value
    act(() => {
      wheel({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
      wheel({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
      host.inventory = { ...host.inventory!, selectedEquipment: 2 };
      renderer!.update(createElement(EquipmentPanel));
    });

    // echo 1後も未確認2から先頭0へ回る
    // After echo 1, wrap from pending 2 to the first slot 0
    act(() => {
      host.inventory = {
        ...host.inventory!,
        selectedEquipment: 1,
        equipmentSelectionConfirmationRevision: 1,
      };
      renderer!.update(createElement(EquipmentPanel));
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(3, "inventory.select_equipment", { index: 0 });
  });

  it("action失敗時はその要求をpendingから除く", async () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    host.dispatchAction.mockResolvedValueOnce(false).mockResolvedValue(true);
    renderSlots();

    await act(async () => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
      await Promise.resolve();
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
    expect(host.dispatchAction).toHaveBeenNthCalledWith(2, "inventory.select_equipment", { index: 1 });
  });

  it("再接続開始時にpendingを破棄して復元snapshotを起点にする", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    let renderer: ReturnType<typeof create>;
    act(() => {
      renderer = create(createElement(EquipmentPanel));
    });
    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    // 切断で0を捨て復元値2から先頭0へ回る
    // Drop 0 on disconnect and wrap from restored 2 to the first slot 0
    act(() => {
      host.connectionStatus = "reconnecting";
      renderer!.update(createElement(EquipmentPanel));
    });
    act(() => {
      host.inventory = { ...host.inventory!, selectedEquipment: 2 };
      host.connectionStatus = "open";
      renderer!.update(createElement(EquipmentPanel));
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(2, "inventory.select_equipment", { index: 0 });
  });

  it("加速で膨らんだ1イベントでも1段しか進まない", () => {
    host.uiState = { state: "GameScreen" };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();

    act(() => {
      host.wheelHandler!({ deltaY: 400, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenCalledTimes(1);
    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
  });

  it("ホイール占有中はホイールで装備が切り替わらない", () => {
    host.uiState = { state: "GameScreen" };
    host.placementMode = { selectedTargetType: "connectTool", selectedConnectToolGuid: "c0000000-0000-4000-8000-000000000001", height: 0, unavailableReason: "", wheelOwnedByTool: true };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();

    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).not.toHaveBeenCalled();
  });

  it("ホイールを読まない接続ツール中は抑止されず装備が切り替わる", () => {
    host.uiState = { state: "GameScreen" };
    host.placementMode = { selectedTargetType: "connectTool", selectedConnectToolGuid: "c0000000-0000-4000-8000-000000000002", height: 0, unavailableReason: "", wheelOwnedByTool: false };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();

    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
  });

  it("BPコピー選択中でもドラッグしていなければ装備が切り替わる", () => {
    host.uiState = { state: "GameScreen" };
    host.placementMode = { selectedTargetType: "blueprintCopy", height: 0, unavailableReason: "", wheelOwnedByTool: false };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();

    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
  });

  it("block選択中は抑止されずホイールで装備が切り替わる", () => {
    host.uiState = { state: "GameScreen" };
    host.placementMode = { selectedTargetType: "block", selectedBlockGuid: "b0000000-0000-4000-8000-000000000001", height: 0, unavailableReason: "", wheelOwnedByTool: false };
    host.inventory!.equipment = [slot(1, 1), slot(2, 1), slot(3, 1)];
    renderSlots();

    act(() => {
      host.wheelHandler!({ deltaY: 100, deltaMode: 0, target: null } as WheelEvent);
    });

    expect(host.dispatchAction).toHaveBeenNthCalledWith(1, "inventory.select_equipment", { index: 1 });
  });
});
