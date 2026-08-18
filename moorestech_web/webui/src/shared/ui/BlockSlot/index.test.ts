import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { describe, expect, it } from "vitest";
import BlockSlot from "./index";
import styles from "./style.module.css";

function renderBlockSlot(name?: string) {
  return renderToStaticMarkup(createElement(MantineProvider, null, createElement(BlockSlot, { blockId: 12, name })));
}

describe("BlockSlot", () => {
  it("従来の暗背景契約を保ちつつブロックアイコンを直下に描画する", () => {
    const markup = renderBlockSlot("Assembler");

    expect(markup).toContain("/api/block-icons/12.png");
    expect(markup).toContain('alt="Assembler"');
    expect(markup).not.toContain("data-filled");
    expect(markup.match(/<div/g)).toHaveLength(1);
    expect(markup.match(/<img/g)).toHaveLength(1);
  });

  it("従来と同じアイコンCSSを適用する", () => {
    const markup = renderBlockSlot("Assembler");

    expect(markup).toContain(`class="${styles.icon}"`);
  });

  it("nameが無ければホバーTooltipを出さない", () => {
    const markup = renderBlockSlot(undefined);

    // MantineのTooltipはdisabled時、対象要素をそのまま素通しする(ラップ要素を追加しない)
    // Mantine's Tooltip passes the target straight through when disabled (adds no wrapper element)
    expect(markup.match(/<div/g)).toHaveLength(1);
  });
});
