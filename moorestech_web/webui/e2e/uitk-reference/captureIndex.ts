import { writeFile } from "node:fs/promises";
import { join } from "node:path";
import type { BundleId, LedgerRow } from "./referenceShots";

export type CaptureEntry = { id: string; bundle: BundleId; ledgerRow: LedgerRow; file: string; kind: "image" | "video" };

// manifest と束ごとの一覧（GitHub でそのまま見られる Markdown）を書く
// Write the manifest and a per-bundle index Markdown that renders on GitHub
export async function writeCaptureIndex(outputDirectory: string, entries: CaptureEntry[], sourceCommit: string, viewport: { width: number; height: number }): Promise<void> {
  await writeFile(join(outputDirectory, "manifest.json"), JSON.stringify({ sourceCommit, viewport: `${viewport.width}x${viewport.height}`, mockHostDemo: true, entries }, null, 2));
  const lines = [`# Web UI 参照キャプチャ（moorestech ${sourceCommit}）`, "",
    "UI Toolkit 移行で Web 版の「意図した見た目」を辿るための撮影。mock-host（DEMO）で撮った。台帳は moorestech の `docs/uitk/TODO.md`。", ""];
  for (const bundle of [1, 2, 3, 4, 5] as const) {
    lines.push(`## 束 ${bundle}`, "");
    for (const entry of entries.filter((candidate) => candidate.bundle === bundle)) {
      lines.push(`### ${entry.id}（${entry.ledgerRow}）`, "", entry.kind === "image" ? `![${entry.id}](${entry.file})` : `動画: [${entry.file}](${entry.file})`, "");
    }
  }
  await writeFile(join(outputDirectory, "index.md"), lines.join("\n"));
}
