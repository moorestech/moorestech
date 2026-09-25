// ModeSwitchの選択状態、向き、クリック契約を検証する
// Verifies ModeSwitch selection, orientation, and click contract
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import ModeSwitch from "./index";

describe("ModeSwitch", () => {
  it("選択中のoptionと縦向きをdata属性で公開する", () => {
    const renderer = create(createElement(ModeSwitch, {
      value: "unload",
      options: [
        { value: "load", label: createElement("span", null, "load"), testId: "load-option" },
        { value: "unload", label: createElement("span", null, "unload") },
      ],
      onChange: () => {},
      orientation: "vertical",
      testId: "mode-switch",
    }));

    const root = renderer.root.findByProps({ "data-testid": "mode-switch" });
    const buttons = renderer.root.findAllByType("button");

    expect(root.props["data-orientation"]).toBe("vertical");
    expect(buttons[0].props["data-testid"]).toBe("load-option");
    expect(buttons[0].props["data-selected"]).toBeUndefined();
    expect(buttons[1].props["data-testid"]).toBeUndefined();
    expect(buttons[1].props["data-selected"]).toBe("true");
  });

  it("disabled指定時はdata-disabledと各buttonのdisabledを公開する", () => {
    const renderer = create(createElement(ModeSwitch, {
      value: "a",
      options: [{ value: "a", label: createElement("span", null, "mode") }],
      onChange: () => {},
      disabled: true,
      testId: "mode-switch",
    }));

    const root = renderer.root.findByProps({ "data-testid": "mode-switch" });
    const button = renderer.root.findByType("button");

    expect(root.props["data-disabled"]).toBe(true);
    expect(button.props.disabled).toBe(true);
  });

  it("クリックしたoptionのvalueを通知する", () => {
    const onChange = vi.fn();
    const renderer = create(createElement(ModeSwitch, {
      value: "a",
      options: [{ value: "b", label: createElement("span", null, "mode") }],
      onChange,
    }));

    act(() => renderer.root.findByType("button").props.onClick());

    expect(onChange).toHaveBeenCalledWith("b");
  });

  it("option単位のdisabledはそのボタンだけを無効化しrootにdata-disabledを付けない", () => {
    const onChange = vi.fn();
    const renderer = create(createElement(ModeSwitch, {
      value: "a",
      options: [
        { value: "a", label: createElement("span", null, "a") },
        { value: "b", label: createElement("span", null, "b"), disabled: true },
      ],
      onChange,
      testId: "mode-switch",
    }));
    const root = renderer.root.findByProps({ "data-testid": "mode-switch" });
    const buttons = renderer.root.findAllByType("button");

    expect(root.props["data-disabled"]).toBeUndefined();
    expect(buttons[0].props.disabled).toBeFalsy();
    expect(buttons[1].props.disabled).toBe(true);
    expect(buttons[1].props["data-option-disabled"]).toBe("true");
  });

  // 旧PanelTabsが守っていた §8.22 タブ様式をここで引き継ぐ
  // Inherits the §8.22 tab contract that the former PanelTabs guarded
  it('role="tablist"指定ではtab/aria-selectedを公開しクリックで値を返す', () => {
    const onChange = vi.fn();
    const renderer = create(createElement(ModeSwitch, {
      value: "timetable",
      options: [
        { value: "inventory", label: "inv", testId: "tab-inventory" },
        { value: "timetable", label: "tt", testId: "tab-timetable" },
      ],
      onChange,
      role: "tablist" as const,
      testId: "tabs",
    }));
    const root = renderer.root.findByProps({ "data-testid": "tabs" });
    const buttons = renderer.root.findAllByType("button");

    expect(root.props.role).toBe("tablist");
    expect(buttons[0].props.role).toBe("tab");
    expect(buttons[0].props["aria-selected"]).toBe(false);
    expect(buttons[0].props["aria-pressed"]).toBeUndefined();
    expect(buttons[1].props["aria-selected"]).toBe(true);
    expect(buttons[1].props["data-selected"]).toBe("true");
    act(() => buttons[0].props.onClick());
    expect(onChange).toHaveBeenCalledWith("inventory");
  });

  // 既定のgroupは択一ボタン列のままでtab語彙を出さない
  // The default group stays an exclusive button row and emits no tab vocabulary
  it("既定ではrole未指定でaria-pressedを保つ", () => {
    const renderer = create(createElement(ModeSwitch, {
      value: "a",
      options: [{ value: "a", label: createElement("span", null, "a") }],
      onChange: () => {},
      testId: "mode-switch",
    }));
    const root = renderer.root.findByProps({ "data-testid": "mode-switch" });
    const button = renderer.root.findByType("button");

    expect(root.props.role).toBeUndefined();
    expect(button.props.role).toBeUndefined();
    expect(button.props["aria-pressed"]).toBe(true);
    expect(button.props["aria-selected"]).toBeUndefined();
  });
});
