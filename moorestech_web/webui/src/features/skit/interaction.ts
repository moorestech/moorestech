export type SkitClickOutcome = "reveal" | "advance" | "none";

export function clickOutcome(visibleCount: number, bodyLength: number, advanceAllowed: boolean): SkitClickOutcome {
  if (visibleCount < bodyLength) return "reveal";
  return advanceAllowed ? "advance" : "none";
}

export function nextRevealCount(body: string, visibleCount: number): number {
  return Math.min(visibleCount + 1, Array.from(body).length);
}

export function shouldRevealImmediately(
  connectionStatus: string,
  previousConnectionStatus: string,
  revealMode: "instant" | "typewriter",
  intervalMs: number,
): boolean {
  return connectionStatus !== "open" || previousConnectionStatus !== "open"
    || revealMode === "instant" || intervalMs === 0;
}
