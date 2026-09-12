// PanelActionButtonの押下契約と無効化のdata属性公開を検証する
// Verifies PanelActionButton's click contract and its disabled data attribute
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import PanelActionButton from "./index";

describe("PanelActionButton", () => {
  it("押すとonClickを呼び、既定ではdata-disabledを出さない", () => {
    const onClick = vi.fn();
    const renderer = create(createElement(PanelActionButton, { onClick, testId: "panel-action", children: "送信" }));
    const button = renderer.root.findByType("button");

    expect(button.props["data-disabled"]).toBeUndefined();
    expect(button.props.disabled).toBeUndefined();
    act(() => button.props.onClick());
    expect(onClick).toHaveBeenCalled();
  });

  it("disabled指定時はdata-disabledとbuttonのdisabledを公開する", () => {
    const renderer = create(createElement(PanelActionButton, { onClick: () => {}, disabled: true, testId: "panel-action", children: "送信" }));
    const button = renderer.root.findByType("button");

    expect(button.props["data-disabled"]).toBe(true);
    expect(button.props.disabled).toBe(true);
  });
});
