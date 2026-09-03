import { readFileSync } from "node:fs";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { itemNameKey } from "@/shared/i18n/contentKeys";
import { L } from "@/shared/i18n/generated/localizationKeys";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import ItemSlot from "./index";
import styles from "./style.module.css";

const ITEM_GUID = "01234567-89ab-cdef-0123-456789abcdef";

// ツールチップ本文は開いてからでないとDOMへ出ないため、静的描画で本文を読めるスタブへ差し替える
// The tooltip body only reaches the DOM once opened, so a stub renders it inline for static markup
vi.mock("../HoverTooltip", () => ({
  default: ({ label, children }: { label?: unknown; children?: unknown }) =>
    createElement("mock-hover-tooltip", null, label as never, children as never),
}));

vi.mock("@/bridge", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/bridge")>(),
  useItemMaster: () => new Map([[1, { itemId: 1, itemGuid: ITEM_GUID, maxStack: 100 }]]),
}));

function renderItemSlot(insufficient?: boolean, count?: number, catalog?: boolean) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(ItemSlot, {
      itemId: 1,
      insufficient,
      count,
      catalog,
    })),
  );
}

function renderShortageSlot(insufficient: boolean) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(ItemSlot, {
      itemId: 1,
      insufficient,
      shortage: { ownedCount: 2, requiredCount: 5, tooltipKey: L.ui.recipe.materialTooltip },
    })),
  );
}

describe("ItemSlot", () => {
  beforeEach(() => {
    const key = itemNameKey(ITEM_GUID);
    setDictionaries(
      "japanese",
      {
        [key]: "辞書アイテム",
        [L.ui.common.itemFallback]: "アイテム {itemId}",
        [L.ui.recipe.itemCountSummary]: "{ownedCount}/{requiredCount}",
        [L.ui.recipe.materialTooltip]: "{itemName}\n所持数: {ownedCount}\n必要数: {requiredCount}",
      },
      { [key]: "Dictionary item", [L.ui.common.itemFallback]: "Item {itemId}" },
      { [key]: "Source item", [L.ui.common.itemFallback]: "Item {itemId}" },
    );
  });

  it("item master のGuidから辞書名をtooltipと代替テキストへ使う", () => {
    const markup = renderItemSlot(undefined);
    expect(markup).toContain("辞書アイテム");
    expect(markup).not.toContain("Source item");
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

  it("countがundefinedの時はバッジを表示しない", () => {
    const markup = renderItemSlot(undefined, undefined);

    expect(markup).not.toMatch(new RegExp(`<span class="[^"]*\\b${styles.count}\\b`));
  });

  // アイコンを描くcatalogでも0はバッジ非表示
  // Even in catalog mode, where the icon renders, a 0 must not render the badge
  it("countが0の時はバッジを表示しない", () => {
    const markup = renderItemSlot(undefined, 0, true);

    expect(markup).toContain("<img");
    expect(markup).not.toMatch(new RegExp(`<span class="[^"]*\\b${styles.count}\\b`));
  });

  it("countが正の数の時はバッジを表示する", () => {
    const markup = renderItemSlot(undefined, 5);

    // 縁は共有クラスの合成で乗る（ADR 0033）
    // The outline arrives through a shared class (ADR 0033)
    expect(markup).toContain(`<span class="iconTextOutlineLight ${styles.count}">5</span>`);
  });

  // 所持と必要の対応がここで入れ替わると全素材表示が同時に狂うため、順序込みで固定する
  // Swapping owned and required here would corrupt every material display at once, so the order is pinned
  it("shortageは所持/必要の順で数値を出し、ツールチップにも同じ対応で載せる", () => {
    const markup = renderShortageSlot(true);

    expect(markup).toContain(`<span class="iconTextOutlineLight ${styles.shortageCount}" data-lack="true">2/5</span>`);
    expect(markup).toContain("辞書アイテム\n所持数: 2\n必要数: 5");
  });

  it("充足している素材のshortageは赤くしない", () => {
    const markup = renderShortageSlot(false);

    expect(markup).toContain("2/5");
    expect(markup).not.toContain("data-lack");
  });
});
