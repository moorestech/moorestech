import { describe, expect, it } from "vitest";
import {
  blockIconUrl,
  fluidIconUrl,
  itemIconUrl,
  itemMasterUrl,
  localizationDictionaryUrl,
} from "./httpEndpoints";

describe("httpEndpoints", () => {
  it("既存のアイテムアイコンURLを維持する", () => {
    expect(itemIconUrl(42)).toBe("/api/icons/42.png");
  });

  it("既存のブロックアイコンURLを維持する", () => {
    expect(blockIconUrl(12)).toBe("/api/block-icons/12.png");
  });

  it("液体アイコンURLをguidから組み立てる", () => {
    expect(fluidIconUrl("60000000-0000-4000-8000-000000000001")).toBe("/api/fluid-icons/60000000-0000-4000-8000-000000000001.png");
  });

  it("既存のアイテムマスタURLを維持する", () => {
    expect(itemMasterUrl).toBe("/api/master/items");
  });

  it("辞書URLへ期待revisionを含める", () => {
    expect(localizationDictionaryUrl("japanese", 42))
      .toBe("/api/i18n/japanese?revision=42");
  });
});
