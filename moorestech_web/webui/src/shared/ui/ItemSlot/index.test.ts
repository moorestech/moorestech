import { readFileSync } from "node:fs";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { describe, expect, it, vi } from "vitest";
import ItemSlot from "./index";

vi.mock("@/bridge", () => ({
  useItemMaster: () => new Map([[1, { itemId: 1, name: "Master Item", maxStack: 100 }]]),
}));

function renderItemSlot(insufficient?: boolean) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(ItemSlot, {
      itemId: 1,
      insufficient,
    })),
  );
}

describe("ItemSlot", () => {
  it("name 省略時は item master の名前を表示に使う", () => {
    expect(renderItemSlot(undefined)).toContain("Master Item");
  });

  it("name 指定時は item master より優先する", () => {
    const markup = renderToStaticMarkup(
      createElement(MantineProvider, null, createElement(ItemSlot, {
        itemId: 1,
        name: "Override Item",
      })),
    );

    expect(markup).toContain("Override Item");
    expect(markup).not.toContain("Master Item");
  });

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
