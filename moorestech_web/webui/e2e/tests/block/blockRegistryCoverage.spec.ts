import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { test, expect } from "@playwright/test";
import { registeredBlockTypes } from "../../../src/features/blockInventory/registry/registeredBlockTypes";

type BlockUiEntry = {
  blockType: string;
  blockUIAddressablesPath: string;
};

// 共通表示の意図的allowlist
// Existing UIs intentionally handled by the common SectionStackView; remove when their unique controls are ported.
const intentionalGeneric = new Set([
  "Chest",
  "CleanRoomMachine",
  "ElectricMachine",
  "ElectricMiner",
  "FuelGearGenerator",
  "GearMachine",
  "GearMiner",
]);

test("v8マスタで専用UIを持つ全blockTypeがレジストリまたは意図的Genericに分類される", () => {
  // 再生成: blocks.jsonの.dataから[blockType, blockUIAddressablesPath]を抽出し、sort -u後に本fixtureへ反映する。
  // Regenerate: extract [blockType, blockUIAddressablesPath] from blocks.json .data, sort -u, then update this fixture.
  const fixturePath = fileURLToPath(new URL("../../fixtures/v8-block-ui-registry.json", import.meta.url));
  const entries = JSON.parse(readFileSync(fixturePath, "utf8")) as BlockUiEntry[];
  const registered = new Set<string>(registeredBlockTypes);
  const dedicatedTypes = entries
    .filter((entry) => entry.blockUIAddressablesPath.length > 0)
    .map((entry) => entry.blockType);

  const missing = [...new Set(dedicatedTypes)]
    .filter((blockType) => !registered.has(blockType) && !intentionalGeneric.has(blockType));
  expect(missing).toEqual([]);
  expect(registered.has("GearEnergyTransformer")).toBe(false);
});
