# Task Completion Checklist for Moorestech

## When Completing Any Code Task

### 1. Compilation Check (MANDATORY)
- **Server changes**: 
  - Run `mcp__moorestech_server__RefreshAssets`
  - Check `mcp__moorestech_server__GetCompileLogs`
- **Client changes**:
  - Run `mcp__moorestech_client__RefreshAssets`
  - Check `mcp__moorestech_client__GetCompileLogs`

### 2. Testing (for Server-side changes)
- Run relevant tests using `mcp__moorestech_server__RunEditModeTests`
- ALWAYS use groupNames parameter with regex to filter tests:
  - Example: `groupNames: ["^Tests\\.UnitTest\\.Core\\."]`
  - Never run all tests (too slow)

### 3. Validation with Gemini
- For complex changes: `gemini -p "Review this implementation: [details]"`
- For design decisions: Ask Gemini for validation
- For debugging: Use Gemini to verify assumptions

### 4. Code Quality Checks
- Verify no try-catch blocks were added
- Check that existing patterns were followed
- Ensure no .meta files were manually created
- Verify proper use of #region for complex methods

### 5. Before Committing (only if requested)
- Review changes with `git diff`
- Check status with `git status`
- Never commit unless explicitly asked by user

## Debug Workflow
When tests fail or compilation errors occur:
1. Use MCP tools to get detailed error logs
2. Add debug logging to isolate issues
3. Consult Gemini for error analysis
4. Fix issues iteratively
5. Re-run compilation and tests

## Important Reminders
- XY Problem awareness - solve root causes, not symptoms
- Backward compatibility not required for new features
- Performance optimization is secondary to working implementation
- Always search existing codebase before creating new systems