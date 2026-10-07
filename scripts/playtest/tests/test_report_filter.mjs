// report-filter.js の純粋ロジックを node で検証する
// Verifies report-filter.js pure logic under node
import assert from "node:assert/strict";
import { test } from "node:test";
import { applyFilters, filterOf, loadFilter, neighbours, saveFilter } from "../dashboard/static/js/report-filter.js";

import { routeHref } from "../dashboard/static/js/core.js";

const R = (id, kind, extra = {}) => ({ id, kind, triage: "excluded", steamId: "s", buildLabel: "b", description: "", testerName: "", readAt: null, ...extra });
const reports = [R("1", "feedback"), R("2", "bug"), R("3", "feedback", { readAt: "x" }), R("4", "bug"), R("5", "feedback")];

test("filterOf keeps only non-empty filter keys", () => {
  assert.deepEqual(filterOf(new URLSearchParams("kind=feedback&q=&foo=1&read=unread")), { kind: "feedback", read: "unread" });
});

test("applyFilters matches kind and read", () => {
  assert.deepEqual(applyFilters(reports, new URLSearchParams("kind=feedback&read=unread")).map((r) => r.id), ["1", "5"]);
});

test("neighbours skip non-matching reports", () => {
  const n = neighbours(reports, reports[2], new URLSearchParams("kind=feedback"));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "5");
});

test("neighbours work when current no longer matches", () => {
  const n = neighbours(reports, reports[2], new URLSearchParams("kind=feedback&read=unread"));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "5");
});

test("neighbours at the ends are null", () => {
  const n = neighbours(reports, reports[0], new URLSearchParams("kind=feedback"));
  assert.equal(n.newer, null);
  assert.equal(n.older.id, "3");
});

test("neighbours without filter use all reports", () => {
  const n = neighbours(reports, reports[1], new URLSearchParams(""));
  assert.equal(n.newer.id, "1");
  assert.equal(n.older.id, "3");
});

test("saveFilter/loadFilter round-trip and tolerate broken storage", (t) => {
  const warnings = t.mock.method(console, "warn", () => {});
  t.after(() => { delete globalThis.localStorage; });
  const store = new Map();
  globalThis.localStorage = { getItem: (k) => store.get(k) ?? null, setItem: (k, v) => store.set(k, v) };
  saveFilter({ kind: "feedback" });
  assert.deepEqual(loadFilter(), { kind: "feedback" });
  saveFilter({});
  assert.deepEqual(loadFilter(), {});
  store.set("playtest-dashboard.report-filter", "{broken");
  assert.deepEqual(loadFilter(), {});
  store.set("playtest-dashboard.report-filter", JSON.stringify({ kind: 1, evil: "x", q: "a" }));
  assert.deepEqual(loadFilter(), { q: "a" });
  for (const value of ["null", "[]", "42", '"text"']) {
    store.set("playtest-dashboard.report-filter", value);
    assert.deepEqual(loadFilter(), {});
  }
  globalThis.localStorage = { getItem: () => { throw new Error("denied"); }, setItem: () => { throw new Error("denied"); } };
  assert.deepEqual(loadFilter(), {});
  saveFilter({ kind: "bug" });
  delete globalThis.localStorage;
  assert.deepEqual(loadFilter(), {});
  saveFilter({ q: "test" });
  assert.equal(warnings.mock.callCount(), 5);
});

test("saveFilter dispatches after storing the filter", (t) => {
  const store = new Map();
  const events = [];
  globalThis.localStorage = { setItem: (key, value) => store.set(key, value) };
  globalThis.dispatchEvent = (event) => {
    assert.equal(store.get("playtest-dashboard.report-filter"), '{"kind":"feedback"}');
    events.push(event.type);
    return true;
  };
  t.after(() => { delete globalThis.localStorage; delete globalThis.dispatchEvent; });

  saveFilter({ kind: "feedback" });
  assert.deepEqual(events, ["dashboard:filter-saved"]);
});

// 全条件の交差と検索対象を確かめ、移動時の取りこぼしを防ぐ
// Check every intersecting condition and searchable field to prevent losses during navigation
test("all six filters intersect and query matches without case sensitivity", () => {
  const row = R("target", "bug", { triage: "candidate", description: "Mixed CASE" });
  const params = new URLSearchParams("kind=bug&triage=candidate&tester=s&build=b&read=unread&q=mixed");
  assert.deepEqual(applyFilters([row], params), [row]);
  for (const key of ["kind", "triage", "tester", "build", "q"]) {
    const mismatch = new URLSearchParams(params);
    mismatch.set(key, "missing");
    assert.deepEqual(applyFilters([row], mismatch), []);
  }
  assert.deepEqual(applyFilters([row], new URLSearchParams("q=TARGET")), [row]);
  const named = R("id", "bug", { testerName: "Tester" });
  assert.deepEqual(applyFilters([named], new URLSearchParams("q=TESTER")), [named]);
});

test("empty kind and read state keep the existing list semantics", () => {
  const broken = R("broken", null);
  assert.deepEqual(applyFilters([broken, ...reports], new URLSearchParams("kind=(none)")), [broken]);
  assert.deepEqual(applyFilters(reports, new URLSearchParams("read=read")), [reports[2]]);
  assert.deepEqual(applyFilters(reports, new URLSearchParams("read=unknown")), reports);
});

// 新しい取得結果で候補が変わり、末端と一致なしではリンクが消える
// New data changes neighbours, while endpoints and no matches produce no links
test("neighbours recompute from latest state and handle both ends", () => {
  const params = new URLSearchParams("kind=feedback&read=unread");
  const updated = reports.map((r) => ({ ...r, readAt: r.id === "1" ? "read" : r.readAt }));
  assert.deepEqual(neighbours(updated, updated[2], params), { newer: null, older: updated[4] });
  assert.deepEqual(neighbours(reports, reports[4], params), { newer: reports[0], older: null });
  assert.deepEqual(neighbours(reports, reports[2], new URLSearchParams("kind=missing")), { newer: null, older: null });
});

test("detail and list routes preserve all filter values including URL punctuation", () => {
  const filter = { triage: "candidate", kind: "bug", read: "unread", tester: "s", build: "b", q: "日本語 ? & #" };
  const params = new URLSearchParams(filter);
  for (const [view, args] of [["report", ["s", "id"]], ["reports", []]]) {
    const href = routeHref(view, args, filterOf(params));
    assert.deepEqual(Object.fromEntries(new URLSearchParams(href.split("?")[1])), filter);
  }
});
