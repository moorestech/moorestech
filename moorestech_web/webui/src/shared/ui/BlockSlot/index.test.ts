import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import BlockSlot from "./index";
import styles from "./style.module.css";

describe("BlockSlot", () => {
  it("従来の暗背景契約を保ちつつブロックアイコンを直下に描画する", () => {
    const markup = renderToStaticMarkup(createElement(BlockSlot, { blockId: 12, name: "Assembler" }));

    expect(markup).toContain("/api/block-icons/12.png");
    expect(markup).toContain('alt="Assembler"');
    expect(markup).not.toContain("data-filled");
    expect(markup.match(/<div/g)).toHaveLength(1);
    expect(markup.match(/<img/g)).toHaveLength(1);
  });

  it("従来と同じアイコンCSSを適用する", () => {
    const slot = BlockSlot({ blockId: 12, name: "Assembler" });

    expect(slot.props.children.props.className).toBe(styles.icon);
  });
});
