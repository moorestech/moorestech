// 報告の絞り込み: 条件の抽出・一致判定・前後探索・最後の条件の記憶（DOM を持たない）
// Report filtering: condition extraction, matching, neighbour lookup and remembering the last condition (no DOM)

export const FILTER_KEYS = ["triage", "kind", "read", "tester", "build", "q"];
export const CANDIDATE_FILTER = { triage: "candidate" };
export const FEEDBACK_FILTER = { kind: "feedback" };
// 種別が空（読めない箱）は value="" だと「すべて」と区別できないので専用の値で表す
// An empty kind (unreadable box) gets its own value, since "" would mean "all"
export const EMPTY_KIND = "(none)";
export const STORAGE_KEY = "playtest-dashboard.report-filter";

export function filterOf(params) {
  return Object.fromEntries(FILTER_KEYS.filter((key) => params.get(key)).map((key) => [key, params.get(key)]));
}

export function readMatches(report, value) {
  if (value === "unread") return !report.readAt;
  if (value === "read") return Boolean(report.readAt);
  return true;
}

function matcher(params) {
  const query = (params.get("q") || "").toLowerCase();
  return (r) =>
    (!params.get("kind") || (r.kind || EMPTY_KIND) === params.get("kind"))
    && (!params.get("triage") || r.triage === params.get("triage"))
    && (!params.get("tester") || r.steamId === params.get("tester"))
    && (!params.get("build") || r.buildLabel === params.get("build"))
    && readMatches(r, params.get("read") || "")
    && (!query || `${r.description || ""} ${r.id} ${r.testerName}`.toLowerCase().includes(query));
}

export function applyFilters(reports, params) {
  return reports.filter(matcher(params));
}

// 見ている報告が条件から外れても（既読にした等）、全件の並びでの位置から一番近い一致を探す
// Even when the shown report no longer matches (e.g. just marked read), search from its position in the full list
export function neighbours(reports, current, params) {
  const matches = matcher(params);
  const index = reports.indexOf(current);
  const newer = reports.slice(0, index).reverse().find(matches) || null;
  const older = reports.slice(index + 1).find(matches) || null;
  return { newer, older };
}

// 記憶はこのブラウザだけの便宜。読めない・壊れている時は記憶なしとして動く
// Memory is a per-browser convenience; unreadable or broken storage just means nothing is remembered
export function saveFilter(filter) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(filter));
  } catch (error) {
    console.warn("[dashboard] filter memory unavailable", error);
  }
  // Node テストには画面のイベント配送口がないため、ブラウザでだけ通知する
  // Node tests have no page event dispatcher, so notify only in the browser
  if (globalThis.dispatchEvent) globalThis.dispatchEvent(new CustomEvent("dashboard:filter-saved"));
}

export function loadFilter() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}");
    return Object.fromEntries(FILTER_KEYS.filter((key) => typeof saved?.[key] === "string" && saved[key]).map((key) => [key, saved[key]]));
  } catch (error) {
    console.warn("[dashboard] filter memory unavailable", error);
    return {};
  }
}
