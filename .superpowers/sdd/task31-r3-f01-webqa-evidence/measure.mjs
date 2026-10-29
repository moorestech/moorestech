// 実DOMの矩形と中心hitを記録。CSSやpointer-eventsは変更しない。
// Record live DOM rects and center hits without changing CSS or pointer-events.
export async function measure(locator) {
  return locator.evaluate(element => {
    const rect = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    const target = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    const range = document.createRange();
    range.selectNodeContents(element);
    const textRect = range.getBoundingClientRect();
    return {
      rect: rect.toJSON(), textRect: textRect.toJSON(),
      clientHeight: element.clientHeight, scrollHeight: element.scrollHeight,
      clientWidth: element.clientWidth, scrollWidth: element.scrollWidth,
      scrollTop: element.scrollTop, text: element.textContent,
      hit: target !== null && element.contains(target),
      hitTarget: target?.getAttribute('data-testid') ?? target?.tagName,
      style: { display: style.display, overflow: style.overflow, color: style.color, pointerEvents: style.pointerEvents, fontSize: style.fontSize },
    };
  });
}

export function inside(inner, outer, tolerance) {
  return inner.x >= outer.x - tolerance && inner.y >= outer.y - tolerance
    && inner.right <= outer.right + tolerance && inner.bottom <= outer.bottom + tolerance;
}
