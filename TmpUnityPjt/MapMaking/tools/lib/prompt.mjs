import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const TOOLS_DIR = resolve(__dirname, "..");

// audit-requirements.md をシステム指示として読み込み
export async function buildSystemPrompt() {
  return readFile(resolve(TOOLS_DIR, "audit-requirements.md"), "utf-8");
}
