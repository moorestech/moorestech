# Suggested Commands for Moorestech Development

## Unity MCP Commands (Server-side)
- `mcp__moorestech_server__RefreshAssets` - Refresh assets and compile server code
- `mcp__moorestech_server__GetCompileLogs` - Check compilation errors/warnings for server
- `mcp__moorestech_server__RunEditModeTests` - Run server tests (use groupNames for filtering)
  - Example: `groupNames: ["^Tests\\.CombinedTest\\."]` for specific namespace

## Unity MCP Commands (Client-side)
- `mcp__moorestech_client__RefreshAssets` - Refresh assets and compile client code
- `mcp__moorestech_client__GetCompileLogs` - Check compilation errors/warnings for client
- `mcp__moorestech_client__GetCurrentConsoleLogs` - Get Unity console logs
- `mcp__moorestech_client__ClearConsoleLogs` - Clear Unity console
- `mcp__moorestech_client__RunPlayModeTests` - Run client play mode tests

## Git Commands
- `git status` - Check current branch and changes
- `git diff` - View unstaged changes
- `git log --oneline -10` - View recent commits
- `git checkout <branch>` - Switch branches
- `git pull` - Update from remote
- `git push` - Push changes to remote

## File System Commands (Darwin/macOS)
- `ls -la` - List files with details
- `find . -name "*.cs"` - Find C# files
- `grep -r "pattern" .` - Search in files (use rg for better performance)
- `rg "pattern"` - Fast search using ripgrep

## Gemini AI Integration
- `gemini -p "<question>"` - Ask Gemini for advice/validation
- Used for: technical validation, design review, debugging help

## Testing Commands
- For server tests: Use MCP tools with regex filtering
- For client tests: Use MCP play mode test tools
- Always filter tests to relevant scope for faster execution