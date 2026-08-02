import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import * as contentKeys from "./contentKeys";
import { challengeCategoryDescriptionKey, type ContentLocalizationKey } from "./contentKeys";
import { challengeCategoryDescriptionKey as barrelChallengeCategoryDescriptionKey } from "./index";
import type { TranslationKey } from "./i18nStore";
// Nodeビルド用mjsは宣言表parserをテストへ直接公開する
// The Node build module exposes the declaration-table parser directly to tests
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseContentKeyCatalog } from "../../../scripts/generate-content-keys.mjs";

const CATALOG_URL = new URL("../../../../../Localization/content_keys.csv", import.meta.url);
const CATALOG: { namespace: string; field: string; sourceMaster: string }[] =
  parseContentKeyCatalog(readFileSync(CATALOG_URL, "utf8"));
const BUILDERS = contentKeys as unknown as Record<string, (guid: string) => string>;

describe("content localization keys", () => {
  it.each(CATALOG.map((definition) => [`${definition.namespace}.${definition.field}`, definition] as const))(
    "builds %s with the C# <namespace>.<lowercase D guid>.<field> format",
    (_name, definition) => {
      const builderName = `${definition.namespace}${definition.field[0].toUpperCase()}${definition.field.slice(1)}Key`;
      const guid = "01234567-89AB-CDEF-0123-456789ABCDEF";

      expect(BUILDERS[builderName]).toBeTypeOf("function");
      expect(BUILDERS[builderName](guid)).toBe(
        `${definition.namespace}.01234567-89ab-cdef-0123-456789abcdef.${definition.field}`,
      );
    },
  );

  it.each([
    ["item", "name"],
    ["fluid", "name"],
    ["connectTool", "name"],
    ["challengeTutorial", "text"],
  ])("declares the %s.%s row that C# and Web both depend on", (contentNamespace, field) => {
    expect(CATALOG).toContainEqual(
      expect.objectContaining({ namespace: contentNamespace, field }),
    );
  });

  it("merges content keys into the translation key contract", () => {
    const contentKey: ContentLocalizationKey = contentKeys.challengeCategoryNameKey("category-guid");
    const translationKey: TranslationKey = contentKey;

    expect(translationKey).toBe("challengeCategory.category-guid.name");
  });

  it("re-exports the generated builders through the i18n barrel", () => {
    expect(barrelChallengeCategoryDescriptionKey("CATEGORY-GUID"))
      .toBe(challengeCategoryDescriptionKey("category-guid"));
  });

  it("rejects strings outside the content key grammar", () => {
    const acceptContentKey = (key: ContentLocalizationKey): ContentLocalizationKey => key;

    // @ts-expect-error -- A content key must match one of the declared namespaces.
    acceptContentKey("not-a-localization-key");
    // @ts-expect-error -- Item keys only expose the name field.
    acceptContentKey("item.guid.title");
  });
});
