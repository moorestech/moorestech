import { describe, it, expect, vi, beforeEach } from "vitest";
import { useToastStore, emitToast } from "./toastStore";

describe("toastStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useToastStore.setState({ toasts: [] });
  });

  it("emitToast で追加され 3秒後に消える", () => {
    emitToast("hello", "info");
    expect(useToastStore.getState().toasts.map((t) => t.message)).toEqual(["hello"]);
    vi.advanceTimersByTime(3000);
    expect(useToastStore.getState().toasts).toEqual([]);
  });

  it("variant を保持する", () => {
    emitToast("boom", "error");
    expect(useToastStore.getState().toasts[0].variant).toBe("error");
  });
});
