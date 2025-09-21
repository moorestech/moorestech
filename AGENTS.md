# Repository Guidelines

## Project Structure & Module Organization
- `moorestech_client/` - Unity client; gameplay scenes in `Assets/Scenes`, UI and input logic under `Assets/Scripts`.
- `moorestech_server/` - Simulation runtime; domain assemblies in `Assets/Scripts/Game.*`, boot code under `Assets/Scripts/Server`.
- `memory-bank/` - Living design notes; read `projectbrief.md` and `activeContext.md` before starting work.
- `VanillaSchema/` - Git submodule with shared YAML and JSON schema; refresh it with `git submodule update --init --remote VanillaSchema`.
- `.github/` and `ai_docs/` - Automation configs and agent briefs; update them alongside process changes.

## Build, Test, and Development Commands
- Use Unity 2022.3.18f1 for every edit to prevent regenerated `.csproj` noise and package drift.
- Quick compile check on macOS or Linux: `./unity-compile.sh moorestech_server` (swap in `moorestech_client` when needed).
- Windows batch check: `Unity.exe -batchmode -projectPath "<repo>\\moorestech_server" -quit -logFile server_compile.log`.
- Manual play workflow: start Play Mode in `moorestech_server`, then open the `MainGame` scene in `moorestech_client`.
- After branch switches, run `git submodule update --init --recursive` so schema data stays in sync.

## Coding Style & Naming Conventions
- `.editorconfig` enforces UTF-8, LF endings, no auto trim, and four-space indentation for C#; YAML, JSON, and shell scripts use two spaces.
- Prefer `var` for locals when the type is obvious, PascalCase for types or constants, and lowerCamelCase for serialized fields.
- Honor ReSharper formatter tags (`@formatter:on` and `@formatter:off`); avoid hand alignment that will be reformatted.
- Move tunable values into `VanillaSchema` instead of scattering literals inside gameplay scripts.

## Testing Guidelines
- Tests sit in `moorestech_server/Assets/Scripts/Tests` and `Tests.Module`; files end with `*Test.cs` and use NUnit plus Unity Test Framework.
- Run edit mode suites through the Unity Test Runner or `Unity.exe -batchmode -projectPath "<repo>\\moorestech_server" -runTests -testPlatform editmode -logFile Tests.log`.
- Add regression coverage next to the feature area, for example block mechanics in `CombinedTest/Core`.
- Stabilize schema heavy tests with fixtures under `Tests.Module/TestMod` to keep data deterministic.

## Commit & Pull Request Guidelines
- Follow existing history: concise present tense commit messages, in Japanese or English, without ticket prefixes.
- Group code, schema, and generated asset updates in one commit so migrations review cleanly.
- Pull requests should explain behavior changes, list verification steps, and link Jira or GitHub issues; include screenshots or short clips for UI edits.
- Check `git status` for stray Library or Logs files before review and call out any skipped tests in the description.
