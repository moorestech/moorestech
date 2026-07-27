// IconButtonのアクセシビリティ契約・残余props伝播・既定アイコンを検証する
// Verifies IconButton accessibility, passthrough props, and the default icon
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import IconButton from "./index";

describe("IconButton", () => {
  it("aria-label、testId、tutorial向け残余propsをbuttonへ渡す", () => {
    const onClick = vi.fn();
    const renderer = create(createElement(IconButton, {
      onClick,
      ariaLabel: "Close panel",
      testId: "close-panel",
      "data-tutorial-anchor": "inventory.close-button",
    }));
    const button = renderer.root.findByType("button");

    expect(button.props.type).toBe("button");
    expect(button.props["aria-label"]).toBe("Close panel");
    expect(button.props["data-testid"]).toBe("close-panel");
    expect(button.props["data-tutorial-anchor"]).toBe("inventory.close-button");

    act(() => button.props.onClick());
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("children省略時は既定の×を描き、渡された場合はそれを描く", () => {
    const defaultIcon = create(createElement(IconButton, { onClick: vi.fn(), ariaLabel: "Close" }));
    expect(defaultIcon.root.findAllByType("path")).toHaveLength(1);

    const custom = create(createElement(IconButton, { onClick: vi.fn(), ariaLabel: "Auto" },
      createElement("svg", { "data-testid": "custom-icon" })));
    expect(custom.root.findByProps({ "data-testid": "custom-icon" })).toBeDefined();
    expect(custom.root.findAllByType("path")).toHaveLength(0);
  });
});
