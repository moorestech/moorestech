import { expect, type Locator } from "@playwright/test";

export async function expectCraftGrip(frame: Locator, expectedOverlaps: boolean) {
  const contract = await frame.evaluate((element) => {
    // 疑似要素からグリップ契約を読む
    // Read grip contract from pseudo-element
    const frameBox = element.getBoundingClientRect();
    const frameStyle = getComputedStyle(element);
    const grip = getComputedStyle(element, "::after");
    const width = Number.parseFloat(grip.width);
    const height = Number.parseFloat(grip.height);
    const right = Number.parseFloat(grip.right);
    const bottom = Number.parseFloat(grip.bottom);
    const paddingRight = frameBox.right - Number.parseFloat(frameStyle.borderRightWidth);
    const paddingBottom = frameBox.bottom - Number.parseFloat(frameStyle.borderBottomWidth);
    // 平行移動をグリップ矩形に反映する
    // Apply translation to grip box
    const transform = new DOMMatrixReadOnly(grip.transform);
    const gripBox = {
      left: paddingRight - right - width + transform.e,
      top: paddingBottom - bottom - height + transform.f,
      right: paddingRight - right + transform.e,
      bottom: paddingBottom - bottom + transform.f,
    };
    // 要素自身または祖先が不可視ならその配下も不可視として扱う
    // Treat descendants as invisible whenever the element itself or an ancestor is hidden
    function isHiddenOrInsideHidden(node: Element): boolean {
      let current: Element | null = node;
      while (current !== null) {
        const style = getComputedStyle(current);
        if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return true;
        current = current.parentElement;
      }
      return false;
    }

    // 全描画矩形でグリップ重なりを判定する
    // Test grip overlap against painted rects
    // テキストは行矩形、要素は要素矩形
    // Use line rects for text, element rects otherwise
    const contentBoxes: DOMRect[] = [];
    const textWalker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    for (let textNode = textWalker.nextNode(); textNode !== null; textNode = textWalker.nextNode()) {
      const parent = textNode.parentElement;
      if (parent === null || isHiddenOrInsideHidden(parent)) continue;
      // 空白のみのテキストノードは余白要素と同じ誤検出源のため除外する
      // Skip whitespace-only text nodes; they are the same false-overlap source as margin-only boxes
      if ((textNode.textContent ?? "").trim() === "") continue;
      const range = document.createRange();
      range.selectNodeContents(textNode);
      contentBoxes.push(...Array.from(range.getClientRects()));
    }
    const visualElements = element.querySelectorAll("img,svg,canvas,input,select,textarea,button");
    for (const visual of Array.from(visualElements)) {
      if (!isHiddenOrInsideHidden(visual)) contentBoxes.push(visual.getBoundingClientRect());
    }
    // 幅か高さが0の矩形は実際には描画されていないため対象から外す
    // Rects with zero width or height are not actually painted, so exclude them
    const visibleContentBoxes = contentBoxes.filter((box) => box.width > 0 && box.height > 0);
    const overlaps = visibleContentBoxes.some((box) =>
      box.left < gripBox.right && box.right > gripBox.left &&
      box.top < gripBox.bottom && box.bottom > gripBox.top);
    return {
      content: grip.content,
      authoredGripSize: frameStyle.getPropertyValue("--craft-grip-size").trim(),
      authoredGripInset: frameStyle.getPropertyValue("--craft-grip-inset").trim(),
      authoredGripOffset: Number.parseFloat(frameStyle.getPropertyValue("--craft-grip-offset")),
      computedWidth: grip.width,
      computedHeight: grip.height,
      right, bottom,
      transform: grip.transform,
      clipPath: grip.clipPath,
      backgroundColor: grip.backgroundColor,
      backgroundImage: grip.backgroundImage,
      boxShadow: grip.boxShadow,
      zIndex: grip.zIndex,
      overlaps,
    };
  });
  expect(contract).toEqual({
    content: "\"\"",
    authoredGripSize: "8.74px",
    authoredGripInset: "6.98px",
    authoredGripOffset: 0.4,
    computedWidth: "8.73438px",
    computedHeight: "8.73438px",
    right: 6.98,
    bottom: 6.98,
    transform: "matrix(1, 0, 0, 1, 0.4, 0.4)",
    clipPath: "polygon(100% 0px, 100% 100%, 0px 100%)",
    backgroundColor: "rgba(134, 136, 152, 0.98)",
    backgroundImage: "none",
    boxShadow: "none",
    // 子要素は.panel > *でz-index 1へ上がるため、グリップは2で常にその前面に立つ
    // Children are lifted to z-index 1 by .panel > *, so the grip must stand at 2 to stay in front
    zIndex: "2",
    overlaps: expectedOverlaps,
  });
}
