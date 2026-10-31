import { MAX_FILE_BYTES } from "../contract";
import { bundlePrefix, type PlaytestKind } from "../keys";
import type { DeclaredFile } from "./fileList";

export const TRANSFER_INDEX = "repo/commit-transfer.json";
export const PARTS_DIRECTORY = "__attachments";

// 分割計画はアップロード済み原文だけを読み、任意パスや欠けた断片を公開しない
// Read only the uploaded transfer plan, never publishing arbitrary paths or missing parts
export async function transferFiles(bucket: R2Bucket, kind: PlaytestKind, subject: string, id: string, files: DeclaredFile[]): Promise<DeclaredFile[] | string> {
  if (!files.some(file => file.path === TRANSFER_INDEX)) return [];
  if (kind !== "report") return "commit transfer is report-only";
  const object = await bucket.get(`${bundlePrefix(kind, subject, id)}/${TRANSFER_INDEX}`);
  if (object === null) return "commit transfer plan is absent";
  let plan: unknown;
  // R2本文は外部JSON境界。破損は公開拒否として呼出側から通知する
  // R2 content is an external JSON boundary; the caller reports corruption as a publication refusal
  try { plan = await object.json(); }
  catch { return "commit transfer plan is not JSON"; }
  return parseTransferPlan(plan);
}

export function parseTransferPlan(plan: unknown): DeclaredFile[] | string {
  if (!Array.isArray(plan) || plan.length === 0 || plan.length > 2) return "invalid commit transfer plan";
  const seen = new Set<string>();
  const result: DeclaredFile[] = [];
  for (const bundle of plan) {
    if (bundle?.path !== "repo/commits.bundle" && bundle?.path !== "repo/master-commits.bundle") return "invalid commit bundle path";
    if (seen.has(bundle.path)) return "duplicate commit bundle path";
    seen.add(bundle.path);
    if (!Number.isSafeInteger(bundle.bytes) || bundle.bytes <= 0 || !Array.isArray(bundle.parts) || bundle.parts.length === 0) return "invalid commit bundle size or parts";
    let total = 0;
    for (const part of bundle.parts) {
      const expected = `${PARTS_DIRECTORY}/${String(result.length).padStart(6, "0")}/chunk`;
      if (part?.path !== expected || !Number.isSafeInteger(part.bytes) || part.bytes <= 0 || part.bytes > MAX_FILE_BYTES ||
          typeof part.sha256 !== "string" || !/^[0-9a-f]{64}$/.test(part.sha256)) return "invalid commit transfer part";
      total += part.bytes;
      if (!Number.isSafeInteger(total)) return "commit transfer size overflow";
      result.push({ path: part.path, bytes: part.bytes });
    }
    if (total !== bundle.bytes) return "commit transfer length mismatch";
  }
  return result;
}
