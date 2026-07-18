# Existing Web UI Screen i18n Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert every Phase D legacy Web UI screen literal to the existing key-as-source-text i18n hook while preserving rendered copy, DOM structure, and behavior.

**Architecture:** Each affected React component obtains `t` from `useI18n` and replaces only user-visible literals with `t("日本語原文")` or named interpolation. The existing `I18nProvider` remains the sole `localization.current` consumer, and the ESLint allowlist becomes empty so the established rule covers every screen.

**Tech Stack:** React 18, TypeScript, Vitest, ESLint, Vite.

## Global Constraints

- Preserve every displayed string, DOM structure, and interaction.
- Use `key=日本語原文`; do not invent a new key namespace.
- Do not change bridge/transport, skit/tutorial/tutorialAnchor, or C# files.
- Do not run E2E tests or create a git commit.

---

### Task 1: Cross-screen locale-switch regression

**Files:**
- Create: `moorestech_web/webui/src/shared/i18n/allScreensI18n.test.ts`

**Interfaces:**
- Consumes: `I18nProvider`, `useI18n`, `setDictionaries`, and `localization.current` dictionary HTTP loading.
- Produces: A Vitest regression that requires an empty legacy allowlist and verifies subscribed visible copy is redrawn after a locale topic change.

- [ ] **Step 1: Write the failing test**

Mock only React's render lifecycle and the bridge topic value, invoke `I18nProvider` for two locale snapshots, and assert the rendered translation changes. Also read `eslint.config.mjs` and require `legacyUnlocalizedFiles = []` so the test is RED before migration.

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm vitest run src/shared/i18n/allScreensI18n.test.ts`

Expected: FAIL because `legacyUnlocalizedFiles` still contains existing screens.

- [ ] **Step 3: Keep the test unchanged through production edits**

The migration must satisfy the test by emptying the allowlist and retaining the existing topic-to-provider-to-hook redraw path.

### Task 2: Existing screen literal conversion

**Files:**
- Modify: every file currently named by `legacyUnlocalizedFiles` that contains visible copy.
- Modify: `moorestech_web/webui/eslint.config.mjs`

**Interfaces:**
- Consumes: `useI18n(): { locale: string; t: (key, values?) => string }`.
- Produces: unchanged rendered source-language copy with all static visible literals passed through `t`.

- [ ] **Step 1: Enumerate lint violations without the allowlist**

Run ESLint with ignore suppression disabled against the listed files and record each visible JSX literal or visible literal attribute.

- [ ] **Step 2: Convert component-local copy**

Add `import { useI18n } from "@/shared/i18n";`, obtain `const { t } = useI18n();`, and replace literals with `t("原文")`. Convert mixed text/value expressions to named interpolation such as `t("容量: {capacity}", { capacity })`.

- [ ] **Step 3: Handle non-component render helpers without changing structure**

Pass `t` into existing top-level helper functions only where a helper produces static visible copy. Leave server-originated item/block/recipe names untouched and add a narrow ESLint disable comment only if the visible-literal rule reports such an expression.

- [ ] **Step 4: Empty the legacy allowlist**

Set `const legacyUnlocalizedFiles = [];` without changing lint scope or rule behavior.

- [ ] **Step 5: Run focused RED-to-GREEN checks**

Run: `pnpm vitest run src/shared/i18n/allScreensI18n.test.ts`

Expected: PASS with the locale switch updating rendered translated copy and no remaining allowlisted screens.

### Task 3: Full verification and audit

**Files:**
- Inspect: all changed files and the final git diff.

**Interfaces:**
- Consumes: repository scripts `test`, `build`, and `lint`.
- Produces: fresh evidence for all completion gates and an exact conversion/exclusion report.

- [ ] **Step 1: Run unit tests**

Run: `pnpm test`

Expected: all Vitest files and tests pass.

- [ ] **Step 2: Run production build**

Run: `pnpm build`

Expected: TypeScript and Vite exit successfully.

- [ ] **Step 3: Run lint with empty allowlist**

Run: `pnpm lint`

Expected: ESLint exits successfully with no visible-literal errors.

- [ ] **Step 4: Audit scope and exclusions**

Use `git diff --name-only`, `git diff --check`, and an `eslint-disable` search to count converted files, confirm forbidden areas are untouched, and list every dynamic-data exclusion with its reason.

## Placement and precedent review

| Item | Placement | Mechanism | Precedent |
|---|---|---|---|
| Screen translations | Existing feature/shared React component | `useI18n` subscription | `features/challenge/ChallengePanel.tsx` |
| Locale propagation test | `shared/i18n` | `I18nProvider` plus `useSyncExternalStore` | `shared/i18n/useI18n.test.ts` |
| Enforcement | Existing root ESLint config | `no-jsx-visible-literal` with empty legacy ignore list | `shared/i18n/noJsxVisibleLiteral.test.ts` |

No new layer, state owner, protocol, data flow, or UI operation is introduced. Existing operations remain alive because only string expression sources change.
