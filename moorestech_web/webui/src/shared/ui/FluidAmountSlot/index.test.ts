import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fluidNameKey } from "@/shared/i18n/contentKeys";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import FluidAmountSlot from "./index";
import styles from "./style.module.css";

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";

// ツールチップ本文は開いてからでないとDOMへ出ないため、静的描画で本文を読めるスタブへ差し替える
// The tooltip body only reaches the DOM once opened, so a stub renders it inline for static markup
vi.mock("../HoverTooltip", () => ({
  default: ({ label, children }: { label?: unknown; children?: unknown }) =>
    createElement("mock-hover-tooltip", null, label as never, children as never),
}));

function renderSlot(amount?: number) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(FluidAmountSlot, { fluidGuid: FLUID_GUID, amount, testId: "fluid-amount" })),
  );
}

describe("FluidAmountSlot", () => {
  beforeEach(() => {
    const key = fluidNameKey(FLUID_GUID);
    setDictionaries("japanese", { [key]: "水" }, { [key]: "Water" }, { [key]: "Water" });
  });

  it("白面のSlotFrameに寸法クラス付きの液体アイコンを描く", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain('data-filled="true"');
    expect(markup).toContain('data-testid="fluid-amount"');
    expect(markup).toContain(`/api/fluid-icons/${FLUID_GUID}.png`);
    expect(markup).toContain(`class="${styles.icon}"`);
  });

  it("量はN0形式のバッジで右下に出し、アイコン文字の白縁クラスを合成する", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain(">1,000<");
    expect(markup).toContain(`iconTextOutlineLight ${styles.amount}`);
  });

  it("amount未指定ならバッジを出さない", () => {
    const markup = renderSlot(undefined);

    expect(markup.match(/<span/g)).toBeNull();
  });

  it("ホバーツールチップに辞書の液体名を出す", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain("<mock-hover-tooltip>水");
  });
});
