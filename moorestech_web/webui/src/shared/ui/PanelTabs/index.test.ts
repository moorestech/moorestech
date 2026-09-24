// PanelTabsの選択状態とクリック契約を検証する
// Verifies PanelTabs selection state and click contract
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import PanelTabs from "./index";

describe("PanelTabs", () => {
  it("選択中タブをdata-selectedで公開しクリックで値を返す", () => {
    const onChange = vi.fn();
    const renderer = create(createElement(PanelTabs, {
      value: "timetable",
      tabs: [
        { value: "inventory", label: "inv", testId: "tab-inventory" },
        { value: "timetable", label: "tt", testId: "tab-timetable" },
      ],
      onChange,
      testId: "tabs",
    }));
    const buttons = renderer.root.findAllByType("button");
    expect(buttons[0].props["data-selected"]).toBeUndefined();
    expect(buttons[1].props["data-selected"]).toBe("true");
    act(() => buttons[0].props.onClick());
    expect(onChange).toHaveBeenCalledWith("inventory");
  });
});
