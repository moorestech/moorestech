# Moorestech Code Style and Conventions

## C# Coding Conventions

### Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `BlockComponent`, `IBlockInventory`)
- **Methods**: PascalCase (e.g., `ProcessData()`, `CalculateResult()`)
- **Private fields**: Underscore prefix with camelCase (e.g., `_instance`, `_blockData`)
- **Public properties**: PascalCase (e.g., `Instance`, `BlockId`)
- **Parameters/Local variables**: camelCase (e.g., `blockPosition`, `itemStack`)
- **Constants**: UPPER_CASE or PascalCase for public consts

### Code Organization
- Use `#region` and local functions for complex methods:
```csharp
public void ComplexMethod()
{
    // Main flow here
    var result = ProcessData();
    
    #region Internal
    
    Data ProcessData()
    {
        // Implementation
    }
    
    #endregion
}
```
- Never write code below `#endregion`

### Null Handling
- Avoid excessive null checks for core components
- Only check nulls for:
  - External data (API, user input)
  - Async load results (Addressables)
- Trust that core systems (MasterHolder, etc.) are never null

### Error Handling
- **NEVER use try-catch blocks** (project rule)
- Use conditional checks and null checks instead
- Let exceptions bubble up for debugging

### Unity-Specific Patterns
- Singletons use Awake() for initialization
- GameObjects are pre-placed, not dynamically created
- No `.meta` files should be manually created

### Testing
- Server-side uses TDD approach
- Test block IDs defined in `ForUnitTestModBlockId.cs`
- Use regex filtering for test execution

### Important Restrictions
- BlockParam classes are auto-generated (don't create manually)
- Always compile after code changes
- Follow existing patterns in large codebase
- Use existing systems rather than creating new concepts

## File Organization
- Server code: `/moorestech_server/Assets/Scripts/`
- Client code: `/moorestech_client/Assets/Scripts/`
- Tests in `Tests` or `Tests.Module` folders
- Interfaces in `.Interface` namespaces

## Documentation Requirements
- Reference guides:
  - Server: `docs/ServerGuide.md`
  - Client: `docs/ClientGuide.md`
  - Protocol: `docs/ProtocolImplementationGuide.md`