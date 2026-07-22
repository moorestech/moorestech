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
});
