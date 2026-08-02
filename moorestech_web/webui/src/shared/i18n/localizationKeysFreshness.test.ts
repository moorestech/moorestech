import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// Nodeビルド用mjsは生成処理をテストへ直接公開する
// The Node build module exposes generation logic directly to tests
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { generateLocalizationKeysSource, parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { generateContentKeysSource, parseContentKeyCatalog } from "../../../scripts/generate-content-keys.mjs";

const CONTENT_KEY_HEADER = "namespace,field,sourceMaster\n";

describe("localizationKeys freshness", () => {
  it("generated file matches the CSV source of truth", () => {
    const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
    const generatedPath = new URL("./generated/localizationKeys.ts", import.meta.url);
    const expected = generateLocalizationKeysSource(parseLocalizationCsv(readFileSync(csvPath, "utf8")));

    expect(readFileSync(generatedPath, "utf8")).toBe(expected);
  });
});

describe("contentKeys freshness", () => {
  it("generated file matches the declaration table source of truth", () => {
    const csvPath = new URL("../../../../../Localization/content_keys.csv", import.meta.url);
    const generatedPath = new URL("./generated/contentKeys.ts", import.meta.url);
    const expected = generateContentKeysSource(parseContentKeyCatalog(readFileSync(csvPath, "utf8")));

    expect(readFileSync(generatedPath, "utf8")).toBe(expected);
  });
});

describe("parseContentKeyCatalog", () => {
  it("maps declaration rows to namespace, field, and supplying Master", () => {
    const catalog = parseContentKeyCatalog(`${CONTENT_KEY_HEADER}item,name,ItemMaster\nresearch,description,ResearchMaster\n`);

    expect(catalog).toEqual([
      { namespace: "item", field: "name", sourceMaster: "ItemMaster" },
      { namespace: "research", field: "description", sourceMaster: "ResearchMaster" },
    ]);
  });

  it.each([
    ["swapped headers", "field,namespace,sourceMaster\n", /namespace, field, and sourceMaster/],
    ["missing column", "namespace,field\n", /namespace, field, and sourceMaster/],
    ["empty table", "", /content_keys\.csv is empty/],
  ])("rejects %s", (_name, csv, expected) => {
    expect(() => parseContentKeyCatalog(csv)).toThrow(expected);
  });

  it.each([
    ["too few columns", "item,name\n", /expected 3, got 2/],
    ["too many columns", "item,name,ItemMaster,extra\n", /expected 3, got 4/],
    ["missing source master", "item,name,\n", /must declare the Master/],
    ["upper camel namespace", "Item,name,ItemMaster\n", /must match \[a-z\]\[A-Za-z0-9\]\*/],
    ["hyphenated namespace", "build-menu,name,ItemMaster\n", /must match \[a-z\]\[A-Za-z0-9\]\*/],
    ["upper camel field", "item,Name,ItemMaster\n", /must match \[a-z\]\[A-Za-z0-9\]\*/],
    ["duplicated row", "item,name,ItemMaster\nitem,name,BlockMaster\n", /Duplicated content key: item\.name/],
  ])("rejects %s", (_name, row, expected) => {
    expect(() => parseContentKeyCatalog(CONTENT_KEY_HEADER + row)).toThrow(expected);
  });
});

describe("generateContentKeysSource", () => {
  it("emits a template literal union and typed builders in declaration order", () => {
    const source = generateContentKeysSource(
      parseContentKeyCatalog(`${CONTENT_KEY_HEADER}fluid,name,FluidMaster\nconnectTool,name,ConnectToolMaster\n`),
    );

    expect(source).toContain("  | `fluid.${string}.name`");
    expect(source).toContain("export const fluidNameKey = (guid: string): ContentLocalizationKey =>");
    expect(source).toContain("  `connectTool.${canonicalGuidSegment(guid)}.name`;");
    expect(source.indexOf("fluidNameKey")).toBeLessThan(source.indexOf("connectToolNameKey"));
  });
});

describe("parseLocalizationCsv", () => {
  it("parses quotes, commas, escaped quotes, and embedded LF", () => {
    const csv = [
      "key,Source,english,japanese",
      'ui.message.body,"Say ""hello"", friend',
      'again","English',
      'again","日本語"',
    ].join("\n");

    expect(parseLocalizationCsv(csv)).toEqual({
      languageCodes: ["english", "japanese"],
      rows: [{
        key: "ui.message.body",
        source: 'Say "hello", friend\nagain',
        texts: ["English\nagain", "日本語"],
      }],
    });
  });

  it("supports CRLF records and converts literal newline escapes in every text column", () => {
    const csv = "key,Source,english,japanese\r\nui.message.body,Source\\nline,English\\nline,\r\n";

    expect(parseLocalizationCsv(csv).rows[0]).toEqual({
      key: "ui.message.body",
      source: "Source\nline",
      texts: ["English\nline", ""],
    });
  });

  it("preserves quoted empty records so malformed column counts cannot disappear", () => {
    const csv = 'key,Source,english\n""\n';

    expect(() => parseLocalizationCsv(csv)).toThrow(/Column count mismatch/);
  });

  it("ignores empty physical lines like the C# parser", () => {
    const csv = "\nkey,Source,english\n\nui.message.body,Source,English\n\n";

    expect(parseLocalizationCsv(csv).rows).toHaveLength(1);
  });

  it.each([
    ["swapped headers", "Source,key,english"],
    ["misspelled key header", "id,Source,english"],
    ["misspelled Source header", "key,source,english"],
  ])("rejects %s", (_name, header) => {
    expect(() => parseLocalizationCsv(`${header}\nui.a,Source,English\n`))
      .toThrow(/key and Source columns/);
  });

  it.each([
    ["duplicate keys", "key,Source,english\nui.a,A,A\nui.a,B,B\n", /Duplicated key: ui\.a/],
    ["too few columns", "key,Source,english\nui.a,A\n", /expected 3, got 2/],
    ["too many columns", "key,Source,english\nui.a,A,A,extra\n", /expected 3, got 4/],
    ["unterminated quotes", 'key,Source,english\nui.a,"A,A\n', /Unterminated quoted field/],
    ["text after a quote", 'key,Source,english\nui.a,"A"x,A\n', /Unexpected character after closing quote/],
    ["quote after text", 'key,Source,english\nui.a,A"B,A\n', /Quote must begin at the start of a field/],
  ])("rejects %s", (_name, csv, expected) => {
    expect(() => parseLocalizationCsv(csv)).toThrow(expected);
  });
});

describe("generateLocalizationKeysSource", () => {
  it("emits nested constants and a key union in CSV order", () => {
    const csv = parseLocalizationCsv(
      "key,Source,english\n" +
      "ui.mainMenu.playLocally,Play,Play\n" +
      "ui.mainMenu.exitGame,Exit,Exit\n" +
      "ui.game.saveGame,Save,Save\n",
    );

    const source = generateLocalizationKeysSource(csv);

    expect(source).toContain('playLocally: "ui.mainMenu.playLocally"');
    expect(source).toContain('exitGame: "ui.mainMenu.exitGame"');
    expect(source).toContain('saveGame: "ui.game.saveGame"');
    expect(source.indexOf('"ui.mainMenu.playLocally"')).toBeLessThan(source.indexOf('"ui.game.saveGame"'));
    expect(source).toContain(
      'export const VanillaLocalizationKeys = ["ui.mainMenu.playLocally", "ui.mainMenu.exitGame"',
    );
    expect(source).toContain(
      "export type VanillaLocalizationKey = typeof VanillaLocalizationKeys[number]",
    );
  });

  it.each([
    [
      "parent before child",
      "key,Source,english\nui.save,Save,Save\nui.save.confirm,Confirm,Confirm\n",
    ],
    [
      "child before parent",
      "key,Source,english\nui.save.confirm,Confirm,Confirm\nui.save,Save,Save\n",
    ],
  ])("rejects leaf and branch collisions: %s", (_name, csv) => {
    expect(() => generateLocalizationKeysSource(parseLocalizationCsv(csv))).toThrow(/both a leaf and a branch/);
  });

  it.each([
    ["empty segment", "ui..close"],
    ["hyphen conversion", "ui.build-menu.close"],
    ["underscore conversion", "ui.build_menu.close"],
    ["uppercase conversion", "ui.BuildMenu.close"],
    ["numeric prefix conversion", "ui.1buildMenu.close"],
  ])("rejects %s instead of allowing identifier conversion collisions", (_name, key) => {
    const csv = parseLocalizationCsv(`key,Source,english\n${key},Close,Close\n`);

    expect(() => generateLocalizationKeysSource(csv)).toThrow(/must match \[a-z\]\[A-Za-z0-9\]\*/);
  });

  it("emits empty key constants and a tuple-derived never type for a header-only CSV", () => {
    const source = generateLocalizationKeysSource(parseLocalizationCsv("key,Source,english\n"));

    expect(source).toContain("export const L = {} as const;");
    expect(source).toContain("export const VanillaLocalizationKeys = [] as const;");
    expect(source).toContain("export type VanillaLocalizationKey = typeof VanillaLocalizationKeys[number];");
  });
});
