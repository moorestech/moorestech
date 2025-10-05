# Agent Manifest — Moorestech Cloud Edition

**Purpose:**  
This manifest defines the operational rules and development philosophy for the Codex Cloud environment of the **Moorestech** project.  
It removes external assistants (such as Gemini) and focuses on Codex’s native capabilities — structured reasoning, task planning, code editing, and validation.

---

## 🧭 Core Development Principles

### 1. Decision and Execution Roles
- **User**: Defines goals, context, and intent.  
- **Codex**: Executes implementation, refactoring, documentation, and testing tasks faithfully and systematically.  
- **No Third-Party Assistants**: Web search and Gemini integration are disabled. All validation and advice come from Codex itself.

### 2. Philosophy
- Prioritize clarity and maintainability over micro-optimisation.  
- Do not pursue backward compatibility during active development; pursue better design first.  
- Optimisation and extensibility may follow after a working baseline is achieved.  
- Always reason from first principles rather than patching surface symptoms (avoid XY-problem behaviour).

---

## 🧩 Project-Wide Engineering Guidelines

### Code Readability
Use `#region` and local (internal) helper functions to clarify long methods:

```csharp
public void ComplexMethod()
{
    var data = ProcessData();
    var result = CalculateResult(data);

    #region Internal
    Data ProcessData() { /* ... */ }
    Result CalculateResult(Data data) { /* ... */ }
    #endregion
}
```

Rules:
- Place no code after `#endregion`.
- Keep the main flow visible; hide details inside the region.
- Prefer small, composable methods over long procedural blocks.

### Null-Handling Policy
Write under the assumption that core systems are non-null.  
Only perform explicit null-checks when interacting with:
- External or asynchronous data sources (API, Addressables, user input)
- Optional subsystems not guaranteed to exist

Avoid redundant `if (x == null)` inside deterministic engine code.

### Singleton Pattern
- Singleton objects must be pre-placed in the scene or Prefab.  
- Initialise `_instance` inside `Awake`.  
- Never dynamically spawn the GameObject from `Instance` accessors.

Example:
```csharp
public class MySingleton : MonoBehaviour
{
    private static MySingleton _instance;
    public static MySingleton Instance => _instance;

    private void Awake() => _instance = this;
}
```

### Use of Existing Systems
Before implementing new logic:
1. Search existing systems in the repository.  
2. Understand architecture and similar features.  
3. Extend or adapt existing modules instead of introducing new global systems.  
4. Avoid duplicating logic hidden under different names.

### Prohibited and Required Actions
- **NEVER** create `.meta` files manually.  
- **ALWAYS** compile after code edits.  
- **ALWAYS** reference the following when modifying:
  - `docs/ServerGuide.md`
  - `docs/ClientGuide.md`
  - `docs/ProtocolImplementationGuide.md`
- **NEVER** use `try-catch` unless absolutely unavoidable; prefer safe conditions and clear assertions.  
- **NEVER** hand-craft `BlockParam` definitions — they are generated automatically via SourceGenerator (`Mooresmaster.Model.BlocksModule`).

---

## 🚆 Train System Development & Testing

This section merges the **Train Testing Playbook** with the project rules to form a unified reference for server-side train features.

### Scope
Applies to:
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game`

Target audience:
- Contributors and AI agents extending the **train**, **rail**, and **station** subsystems.

### Environment Bootstrapping
```csharp
var env = TrainTestHelper.CreateEnvironment();
var worldBlockDatastore = env.WorldBlockDatastore;
var railGraphDatastore = env.GetRailGraphDatastore();
```
- Never touch `ServerContext` before DI setup completes.  
- For save/load scenarios:  
  - Save JSON → destroy environment → re-bootstrap a new one.

### Block & Rail Placement
- Use IDs from `Tests.Module.TestMod.ForUnitTestModBlockId`.  
- Prefer helper methods like `TrainTestHelper.PlaceRail(...)`.  
- Always assert `Assert.IsNotNull()` after critical placements.

### Train Simulation Patterns
- Build realistic `TrainCar` collections with explicit `RailPosition`.  
- Advance with deterministic time steps (`UpdateTrainByTime`, fixed `deltaTime`).  
- Guard long loops with counters and fail explicitly if a target is never reached:
  ```csharp
  Assert.Fail("Train did not reach destination within N frames");
  ```
- Never rely on `Debug.Log` output for verification.

### Save/Load
After saving:
```csharp
RailGraphDatastore.ResetInstance();
```
Then reload into a fresh environment and re-resolve dependencies.

### Randomness and Determinism
- Use seeded RNG or bounded random loops.  
- Always assert with quantitative thresholds (`Assert.LessOrEqual`, etc.).  
- Avoid stochastic expectations in unit tests.

### Maintenance & Refactoring
- Group helper functions under `#region Internal`.  
- Replace ad-hoc DI setups with `TrainTestHelper.CreateEnvironment()`.  
- Keep test surfaces small and explicit (`[Test]` methods should describe behaviour clearly).  
- Refactor older tests to use shared helpers.

### Running Tests
Run the MCP pipeline in sequence:
1. `mcp__moorestech_server__RefreshAssets`
2. `mcp__moorestech_server__GetCompileLogs`
3. `mcp__moorestech_server__RunEditModeTests`

Use regex filters (e.g., `^Tests\.UnitTest\.Game\.SimpleTrainTestUpdateTrain$`) to narrow scope.

### Common Pitfalls
- Failing to reset singletons between environments.  
- Creating new block IDs without updating `ForUnitTestModBlockId`.  
- Leaving persistent trains across tests.  
- Manually creating `.meta` files.  

---

## 🧪 Testing and Debugging Policy

- Use Codex’s **built-in test execution and diff tools only**.  
- Do not depend on external frameworks beyond Unity Test Framework and standard NUnit.  
- Prefer explicit assertion messages and deterministic update loops.  
- Minimise side effects: each test must be self-contained.

Example snippet:
```csharp
for (int frame = 0; frame < 500; frame++)
{
    env.Update();
    if (train.ReachedDestination) break;
}
Assert.That(train.ReachedDestination, "Train failed to reach station within 500 frames.");
```

---

## 📄 Documentation & Updates
- This file is the **authoritative rule set** for Codex Cloud operation.  
- Update it whenever a new subsystem guideline is confirmed or an obsolete instruction is removed.  
- Keep comments in English; use clear identifiers and consistent formatting.

---

**End of Document**
