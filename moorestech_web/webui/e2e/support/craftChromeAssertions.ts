import { expect, type Locator } from "@playwright/test";

export async function expectCraftGrip(frame: Locator, expectedOverlaps: boolean) {
  const contract = await frame.evaluate((element) => {
    // 疑似要素の計算済みスタイルからグリップ契約を抽出する
    // Extract the grip contract from the pseudo-element's computed style
    const frameBox = element.getBoundingClientRect();
    const frameStyle = getComputedStyle(element);
    const grip = getComputedStyle(element, "::after");
    const width = Number.parseFloat(grip.width);
    const height = Number.parseFloat(grip.height);
    const right = Number.parseFloat(grip.right);
    const bottom = Number.parseFloat(grip.bottom);
    const gripBox = {
      left: frameBox.right - right - width,
      top: frameBox.bottom - bottom - height,
      right: frameBox.right - right,
      bottom: frameBox.bottom - bottom,
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

    // タグ列挙をやめ、実際に描画される矩形を漏れなく集めてグリップとの重なりを判定する
    // Drop the tag whitelist and collect every actually-painted rect to test grip overlap
    // テキストノードは行矩形を、テキストを持たない描画要素は要素矩形を採る
    // Text nodes contribute line rects; non-text visual elements contribute their own bounding rect
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
      computedWidth: grip.width,
      computedHeight: grip.height,
      right, bottom,
      clipPath: grip.clipPath,
      backgroundColor: grip.backgroundColor,
      backgroundImage: grip.backgroundImage,
      boxShadow: grip.boxShadow,
      overlaps,
    };
  });
  expect(contract).toEqual({
    content: "\"\"",
    authoredGripSize: "9.2px",
    computedWidth: "9.1875px",
    computedHeight: "9.1875px",
    right: 7,
    bottom: 7,
    clipPath: "polygon(100% 0px, 100% 100%, 0px 100%)",
    backgroundColor: "rgba(134, 136, 152, 0.98)",
    backgroundImage: "none",
    boxShadow: "none",
    overlaps: expectedOverlaps,
  });
}
