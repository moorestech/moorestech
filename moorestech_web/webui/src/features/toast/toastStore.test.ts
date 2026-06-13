import { describe, it, expect, vi, beforeEach } from "vitest";
import { useToastStore, emitToast } from "./toastStore";

describe("toastStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useToastStore.setState({ toasts: [] });
  });

  it("emitToast で追加され 3秒後に消える", () => {
    emitToast("hello");
    expect(useToastStore.getState().toasts.map((t) => t.message)).toEqual(["hello"]);
    vi.advanceTimersByTime(3000);
    expect(useToastStore.getState().toasts).toEqual([]);
  });
});
