import { createElement, type MouseEvent } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import SlotFrame, { renderSlotFrame } from "./index";

describe("SlotFrame", () => {
  it("data属性と子要素を同じdivへ設定する", () => {
    const markup = renderToStaticMarkup(createElement(SlotFrame, {
      selected: true,
      filled: true,
      catalog: true,
      testId: "slot-frame",
      children: createElement("span", null, "icon"),
    }));

    expect(markup).toContain('data-testid="slot-frame"');
    expect(markup).toContain('data-selected="true"');
    expect(markup).toContain('data-filled="true"');
    expect(markup).toContain('data-catalog="true"');
    expect(markup).toContain("<span>icon</span>");
  });

  it("左右押下とダブルクリックを既存契約どおり振り分ける", () => {
    const onLeftDown = vi.fn();
    const onRightDown = vi.fn();
    const onDoubleClick = vi.fn();
    const frame = renderSlotFrame({ onLeftDown, onRightDown, onDoubleClick }, null);
    const preventDefault = vi.fn();

    frame.props.onMouseDown({ button: 0, shiftKey: true, preventDefault } as unknown as MouseEvent<HTMLDivElement>);
    frame.props.onMouseDown({ button: 2, shiftKey: false, preventDefault } as unknown as MouseEvent<HTMLDivElement>);
    frame.props.onDoubleClick();
    frame.props.onContextMenu({ preventDefault } as unknown as MouseEvent<HTMLDivElement>);

    expect(onLeftDown).toHaveBeenCalledWith(true);
    expect(onRightDown).toHaveBeenCalledOnce();
    expect(onDoubleClick).toHaveBeenCalledOnce();
    expect(preventDefault).toHaveBeenCalledTimes(3);
  });
});
