import { readFileSync } from "node:fs";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { describe, expect, it } from "vitest";
import ItemSlot from "./index";

function renderItemSlot(insufficient?: boolean) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(ItemSlot, {
      itemId: 1,
      insufficient,
    })),
  );
}

describe("ItemSlot", () => {
  it("不足状態をスロット枠のdata属性へ伝える", () => {
    const markup = renderItemSlot(true);

    expect(markup).toContain('data-insufficient="true"');
  });

  it("不足状態の省略時はdata属性を付けない", () => {
    expect(renderItemSlot(undefined)).not.toContain("data-insufficient");
    expect(renderItemSlot(false)).not.toContain("data-insufficient");
  });

  it("不足属性へ従来と同じ40%減光を設定する", () => {
    const css = readFileSync(new URL("../SlotFrame/style.module.css", import.meta.url), "utf8");

    expect(css).toMatch(/\.slot\[data-insufficient="true"\]\s*\{\s*opacity:\s*0\.4;/);
  });
});
