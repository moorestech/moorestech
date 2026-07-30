import { expect, type Locator } from "@playwright/test";

export async function expectCraftGrip(frame: Locator) {
  const contract = await frame.evaluate((element) => {
    // 疑似要素の計算済みスタイルからグリップ契約を抽出する
    // Extract the grip contract from the pseudo-element's computed style
    const frameBox = element.getBoundingClientRect();
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
    // 表示中の内容矩形とグリップが重ならないことを確認する
    // Check that the grip does not overlap any visible content rectangle
    const contentBoxes = Array.from(element.querySelectorAll("button,h1,h2,h3,p,img"))
      .filter((child) => getComputedStyle(child).display !== "none")
      .map((child) => child.getBoundingClientRect());
    const overlaps = contentBoxes.some((box) =>
      box.left < gripBox.right && box.right > gripBox.left &&
      box.top < gripBox.bottom && box.bottom > gripBox.top);
    return {
      content: grip.content,
      width, height, right, bottom,
      clipPath: grip.clipPath,
      backgroundColor: grip.backgroundColor,
      backgroundImage: grip.backgroundImage,
      boxShadow: grip.boxShadow,
      overlaps,
    };
  });
  expect(contract).toEqual({
    content: "\"\"",
    width: 9,
    height: 9,
    right: 7,
    bottom: 7,
    clipPath: "polygon(100% 0px, 100% 100%, 0px 100%)",
    backgroundColor: "rgba(146, 148, 167, 0.98)",
    backgroundImage: "none",
    boxShadow: "none",
    overlaps: false,
  });
}
