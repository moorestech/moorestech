import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

// C# NUnit(WireContract*Test) と同一のフィクスチャを参照する単一ソース。読込口をここ1箇所に閉じる
// Single source shared with the C# NUnit (WireContract*Test); the load path is confined to this one place
const fixturesDir = fileURLToPath(
  new URL("../../../../../moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/", import.meta.url),
);

export function loadFixture(name: string): unknown {
  return JSON.parse(readFileSync(fixturesDir + name, "utf8"));
}
