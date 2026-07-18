const OFFSET = 12;

export function clampTooltipPosition(
  pointerX: number,
  pointerY: number,
  tooltipWidth: number,
  tooltipHeight: number,
  viewportWidth: number,
  viewportHeight: number,
): { x: number; y: number } {
  return {
    x: Math.max(OFFSET, Math.min(pointerX + OFFSET, viewportWidth - tooltipWidth - OFFSET)),
    y: Math.max(OFFSET, Math.min(pointerY + OFFSET, viewportHeight - tooltipHeight - OFFSET)),
  };
}
