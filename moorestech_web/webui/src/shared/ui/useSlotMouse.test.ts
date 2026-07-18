import type { MouseEvent } from "react";
import { describe, expect, it, vi } from "vitest";
import { useSlotMouse } from "./useSlotMouse";

describe("useSlotMouse", () => {
  it("左右押下とコンテキストメニューを共通契約どおり処理する", () => {
    const onLeftDown = vi.fn();
    const onRightDown = vi.fn();
    const preventDefault = vi.fn();
    const handlers = useSlotMouse(onLeftDown, onRightDown);

    handlers.onMouseDown({ button: 0, shiftKey: true, preventDefault } as unknown as MouseEvent<HTMLElement>);
    handlers.onMouseDown({ button: 2, shiftKey: false, preventDefault } as unknown as MouseEvent<HTMLElement>);
    handlers.onContextMenu({ preventDefault } as unknown as MouseEvent<HTMLElement>);

    expect(onLeftDown).toHaveBeenCalledWith(true);
    expect(onRightDown).toHaveBeenCalledOnce();
    expect(preventDefault).toHaveBeenCalledTimes(3);
  });

  it("右ボタンを押したまま進入した時だけ右ドラッグ処理を呼ぶ", () => {
    const onRightEnter = vi.fn();
    const handlers = useSlotMouse(undefined, undefined, onRightEnter);

    handlers.onMouseEnter({ buttons: 2 } as unknown as MouseEvent<HTMLElement>);
    handlers.onMouseEnter({ buttons: 0 } as unknown as MouseEvent<HTMLElement>);

    expect(onRightEnter).toHaveBeenCalledOnce();
  });
});
