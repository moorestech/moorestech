import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fluidNameKey } from "@/shared/i18n/contentKeys";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import FluidAmountSlot from "./index";
import styles from "./style.module.css";

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";

// 本文はopen後にDOM反映のためスタブ化
// Stubbed since the body only reaches the DOM after opening
vi.mock("../HoverTooltip", () => ({
  default: ({ label, children }: { label?: unknown; children?: unknown }) =>
    createElement("mock-hover-tooltip", null, label as never, children as never),
}));

function renderSlot(amount: number, badgeShown: boolean) {
  const badge = badgeShown ? { kind: "show" as const, amount } : { kind: "hidden" as const };
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(FluidAmountSlot, { fluidGuid: FLUID_GUID, badge, testId: "fluid-amount" })),
  );
}

describe("FluidAmountSlot", () => {
  beforeEach(() => {
    const key = fluidNameKey(FLUID_GUID);
    setDictionaries("japanese", { [key]: "水" }, { [key]: "Water" }, { [key]: "Water" });
  });

  it("白面のSlotFrameに寸法クラス付きの液体アイコンを描く", () => {
    const markup = renderSlot(1000, true);

    expect(markup).toContain('data-filled="true"');
    expect(markup).toContain('data-testid="fluid-amount"');
    expect(markup).toContain(`/api/fluid-icons/${FLUID_GUID}.png`);
    expect(markup).toContain(`class="${styles.icon}"`);
  });

  it("量はN0形式のバッジで右下に出し、アイコン文字の白縁クラスを合成する", () => {
    const markup = renderSlot(1000, true);

    expect(markup).toContain(">1,000<");
    expect(markup).toContain(`iconTextOutlineLight ${styles.amount}`);
  });

  // 0量と非表示を取り違えると「量0のレシピ」が無言で空欄になる
  // Confusing a zero amount with a hidden badge would silently blank out a zero-amount recipe
  it("量0はバッジを出して0と描く", () => {
    const markup = renderSlot(0, true);

    expect(markup).toContain('data-testid="fluid-amount-amount"');
    expect(markup).toContain(">0<");
  });

  it("バッジ非表示なら量があってもバッジを出さない", () => {
    const markup = renderSlot(1000, false);

    expect(markup).not.toContain('data-testid="fluid-amount-amount"');
  });

  it("ホバーツールチップに辞書の液体名を出す", () => {
    const markup = renderSlot(1000, true);

    expect(markup).toContain("<mock-hover-tooltip>水");
  });

  // 未解決キーの[!key]プレースホルダを液体名として抱えると、ツールチップが辞書欠落を名前として読み上げる
  // Treating an unresolved [!key] placeholder as the fluid's name makes the tooltip read a missing dictionary entry aloud as a name
  it("液体名が辞書に無ければツールチップのラベルを持たない", () => {
    setDictionaries("japanese", {}, {}, {});

    const markup = renderSlot(1000, true);

    expect(markup).toContain("<mock-hover-tooltip><div");
    expect(markup).not.toContain(`<mock-hover-tooltip>[!${fluidNameKey(FLUID_GUID)}]`);
  });
});
