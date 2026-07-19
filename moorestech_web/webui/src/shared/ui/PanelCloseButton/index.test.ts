// PanelCloseButtonのアクセシビリティ契約と残余props伝播を検証する
// Verifies PanelCloseButton accessibility and passthrough props
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import PanelCloseButton from "./index";

describe("PanelCloseButton", () => {
  it("aria-label、testId、tutorial向け残余propsをbuttonへ渡す", () => {
    const onClick = vi.fn();
    const renderer = create(createElement(PanelCloseButton, {
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
});
