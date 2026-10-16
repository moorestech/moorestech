import type { ServerResponse } from "node:http";
import { readFileSync } from "node:fs";
import * as fx from "../fixtures";
// JavaScriptのcodegen parserには型宣言がない
// The JavaScript codegen parser has no type declarations
// @ts-expect-error Importing the plain ESM parser is intentional
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";

const dictionaries = createDictionaries();

export function serveDictionary(url: string, response: ServerResponse): void {
  const locale = url.split("/api/i18n/")[1]?.split("?")[0] ?? "japanese";
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end(JSON.stringify(dictionaries.get(locale) ?? {}));
}

function createDictionaries(): Map<string, Record<string, string>> {
  const csvUrl = new URL("../../../../../Localization/localization.csv", import.meta.url);
  const csv = parseLocalizationCsv(readFileSync(csvUrl, "utf8"));
  const result = new Map<string, Record<string, string>>();

  // Sourceと各言語を本番CSVの同じ行集合から組み立てる
  // Build Source and every locale from the same production CSV rows
  result.set("source", {
    ...Object.fromEntries(csv.rows.map((row: { key: string; source: string }) => [row.key, row.source])),
    ...fx.itemNameDictionaries.source,
    ...fx.blockNameDictionaries.source,
    ...fx.contentLocalizationDictionaries.source,
  });
  for (let languageIndex = 0; languageIndex < csv.languageCodes.length; languageIndex += 1) {
    const languageCode = csv.languageCodes[languageIndex];
    // 言語追加でfixture側が欠けたとき黙ってspreadがno-opになるため、ここで落として気付けるようにする
    // A missing fixture would make the spread a silent no-op on a new language, so fail loudly here instead
    for (const [name, dictionaries] of [
      ["itemNameDictionaries", fx.itemNameDictionaries],
      ["blockNameDictionaries", fx.blockNameDictionaries],
      ["contentLocalizationDictionaries", fx.contentLocalizationDictionaries],
    ] as const) {
      if (dictionaries[languageCode] === undefined) throw new Error(`${name} has no entry for ${languageCode}`);
    }
    result.set(
      languageCode,
      {
        ...Object.fromEntries(csv.rows.map((row: { key: string; texts: string[] }) => [row.key, row.texts[languageIndex]])),
        ...fx.itemNameDictionaries[languageCode],
        ...fx.blockNameDictionaries[languageCode],
        ...fx.contentLocalizationDictionaries[languageCode],
      },
    );
  }
  return result;
}
