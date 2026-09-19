import { isAcked } from "../bundleMarkers";
import type { PlaytestKind } from "../keys";
import { classifyRedeclaration, readDeclaration, replaceDeclarationIfUnchanged, writeDeclarationIfAbsent, type Declaration } from "./bundleDeclaration";

export type DeclarationSettlement =
  | { kind: "settled" }
  | { kind: "acked" }
  | { kind: "conflict"; detail: string }
  | { kind: "unreadable"; reason: string };

// 要求された宣言をDECLAREDへ確定する。書き込みは全て条件付きで、同時prepareに負けたら1回だけ読み直して判定し直す
// Settles the requested declaration into DECLARED; every write is conditional, and losing to a racing prepare re-reads and re-judges once
export async function settleDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, requested: Declaration): Promise<DeclarationSettlement> {
  const label = `${steamId}/${id}`;
  for (let attempt = 0; attempt < 2; attempt++) {
    const stored = await readDeclaration(bucket, kind, steamId, id);
    if (stored.state === "unreadable") return { kind: "unreadable", reason: stored.reason };

    // 保存済みとの関係で、書かない・拒否・置き換えを決める
    // Relative to the stored declaration, decide between no write, refusal and replacement
    if (stored.state === "ok") {
      const relation = classifyRedeclaration(stored.declaration, requested);
      if (relation === "conflict") return { kind: "conflict", detail: describeConflict(stored.declaration, requested) };
      if (relation === "same") return { kind: "settled" };
    }

    // 本文の読み取り中にACKされうるので、書く直前に確かめ直す
    // An ack may land while the body is read, so re-check right before writing
    if (await isAcked(bucket, kind, steamId, id)) return { kind: "acked" };
    const written = stored.state === "absent"
      ? await writeDeclarationIfAbsent(bucket, kind, steamId, id, requested)
      : await replaceDeclarationIfUnchanged(bucket, kind, steamId, id, requested, stored.etag);
    if (written) {
      if (stored.state === "ok") console.warn(`[upload] ${label} re-declared (shrink) from generation ${stored.declaration.generation} (${stored.declaration.files.length} files) to ${requested.generation} (${requested.files.length} files)`);
      return { kind: "settled" };
    }
    console.warn(`[upload] ${label} lost a conditional DECLARED write to a concurrent prepare (attempt ${attempt + 1}); re-reading`);
  }
  return { kind: "conflict", detail: "lost the conditional DECLARED write to concurrent prepares twice" };
}

function describeConflict(stored: Declaration, requested: Declaration): string {
  if (requested.generation < stored.generation) return `generation ${requested.generation} is older than the stored ${stored.generation}`;
  if (requested.generation === stored.generation) return `generation ${requested.generation} declares a different file set than the stored one`;
  return `generation ${requested.generation} adds or resizes files against the stored generation ${stored.generation}`;
}
