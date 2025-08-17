# Moorestech Development Principles

## Three-Part Development Approach (三位一体)
1. **User**: Decision maker - defines goals and requirements
2. **Claude**: Executor - implements, refactors, manages tasks
3. **Gemini**: Advisor - provides validation, web search, technical advice

## Core Development Philosophy

### Build on Existing Systems
- This is a LARGE codebase - most functionality already exists
- Always search thoroughly before creating new systems
- Understand existing patterns before implementing
- Use these search strategies:
  - Symbol search with `mcp__serena__find_symbol`
  - Pattern search with `mcp__serena__search_for_pattern`
  - File exploration with `mcp__serena__get_symbols_overview`

### Pragmatic Approach
- Backward compatibility NOT required
- Performance optimization is secondary
- Working implementation first, optimize later
- Avoid XY problem - solve root causes

### Code Quality Guidelines
- Readability over cleverness
- Use #region and local functions for complex logic
- Minimal null checking (trust core systems)
- No defensive programming for internal components
- Follow existing patterns religiously

### Unity-Specific Rules
- Never manually create .meta files
- Singletons initialized in Awake()
- GameObjects are pre-placed, not dynamically created
- Use Addressables for asset management

### Testing Strategy
- Server: TDD is mandatory
- Client: Focus on compilation, not tests
- Always filter tests to relevant scope
- Debug with logs when tests fail

### Communication with AI Tools
- Use Gemini (`gemini -p`) for:
  - Technical validation
  - Error analysis
  - Design review
  - Latest documentation lookup
- Never use Claude's WebSearch (use Gemini instead)
- Get multiple perspectives from Gemini by rephrasing

### Error Handling Philosophy
- No try-catch blocks (let errors surface)
- Use conditional logic for control flow
- Debug logs for troubleshooting
- Clear error messages for debugging

### Documentation References
Always consult:
- `docs/ServerGuide.md` for server implementation
- `docs/ClientGuide.md` for client implementation
- `docs/ProtocolImplementationGuide.md` for protocol work
- `CLAUDE.md` for project-specific instructions