import { describe, expect, it } from "vitest";
import { blockNameKey, connectToolNameKey, trainCarNameKey, L } from "@/shared/i18n";
import { localizeSelectableTargetName, placementTargetOf } from "./selectableTarget";

const blockGuid = "30000000-0000-4000-8000-000000000001";
const connectToolGuid = "40000000-0000-4000-8000-000000000001";
const trainCarGuid = "8f9c2a51-0000-4000-8000-000000000001";
const blueprintGuid = "3f6a9c1e-8b2d-4f7a-9e3c-1a2b3c4d5e6f";

describe("localizeSelectableTargetName", () => {
  it("blockはblockNameKeyで解決する", () => {
    expect(localizeSelectableTargetName(
      { type: "block", guid: blockGuid },
      (key) => (key === blockNameKey(blockGuid) ? "木のチェスト" : "unused"),
    )).toBe("木のチェスト");
  });

  it("connectToolはconnectToolNameKeyで解決する", () => {
    expect(localizeSelectableTargetName(
      { type: "connectTool", guid: connectToolGuid },
      (key) => (key === connectToolNameKey(connectToolGuid) ? "電線ツール" : "unused"),
    )).toBe("電線ツール");
  });

  it("trainCarはtrainCarNameKeyで解決する", () => {
    expect(localizeSelectableTargetName(
      { type: "trainCar", guid: trainCarGuid },
      (key) => (key === trainCarNameKey(trainCarGuid) ? "蒸気機関車" : "unused"),
    )).toBe("蒸気機関車");
  });

  it("blueprintCopyはtyped UI keyで解決する", () => {
    expect(localizeSelectableTargetName(
      { type: "blueprintCopy" },
      (key) => (key === L.ui.buildMenu.blueprintCopy ? "ブループリントコピー" : "unused"),
    )).toBe("ブループリントコピー");
  });

  it("rawはユーザー命名文字列をそのまま返す", () => {
    expect(localizeSelectableTargetName({ type: "raw", label: "starter-base" }, () => "unused"))
      .toBe("starter-base");
  });
});

describe("placementTargetOf", () => {
  it("辞書解決kindはidをguidとして写す", () => {
    expect(placementTargetOf({ kind: "block", id: blockGuid })).toEqual({ type: "block", guid: blockGuid });
    expect(placementTargetOf({ kind: "connectTool", id: connectToolGuid })).toEqual({ type: "connectTool", guid: connectToolGuid });
    expect(placementTargetOf({ kind: "trainCar", id: trainCarGuid })).toEqual({ type: "trainCar", guid: trainCarGuid });
  });

  it("blueprintCopyはidを持たない共通種別になる", () => {
    expect(placementTargetOf({ kind: "blueprintCopy" })).toEqual({ type: "blueprintCopy" });
  });

  it("保存BPだけが原文labelのrawになる", () => {
    expect(placementTargetOf({ kind: "blueprint", label: "starter-base" })).toEqual({ type: "raw", label: "starter-base" });
  });

  it("ホットバー枠のDTO形状もそのまま受け付ける", () => {
    const hotbarSlot = { kind: "block", id: blockGuid, iconUrl: "/block-icons/1.png" } as const;
    expect(placementTargetOf(hotbarSlot)).toEqual({ type: "block", guid: blockGuid });
    const hotbarBlueprintSlot = { kind: "blueprint", id: blueprintGuid, label: "starter-base" } as const;
    expect(placementTargetOf(hotbarBlueprintSlot)).toEqual({ type: "raw", label: "starter-base" });
  });
});
