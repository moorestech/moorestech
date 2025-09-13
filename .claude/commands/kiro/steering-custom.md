---
description: Create custom Kiro steering documents for specialized project contexts
allowed-tools: Bash, Read, Write, Edit, MultiEdit, Glob, Grep, LS
---

# Kiro Custom Steering Creation

Create custom steering documents in `.kiro/steering/` for specialized contexts beyond the three foundational files (product.md, tech.md, structure.md).

## Current Steering Status

### Existing Steering Documents
- Core steering files: !`ls -la .kiro/steering/*.md 2>/dev/null || echo "No steering directory found"`
- Custom steering count: !`if [ -d ".kiro/steering" ]; then count=0; for f in .kiro/steering/*.md; do if [ -f "$f" ] && [ "$f" != ".kiro/steering/product.md" ] && [ "$f" != ".kiro/steering/tech.md" ] && [ "$f" != ".kiro/steering/structure.md" ]; then count=$((count + 1)); fi; done; echo "$count"; else echo "0"; fi`

### Project Analysis
- Specialized areas: !`find . -path ./node_modules -prune -o -path ./.git -prune -o -type d \( -name "test*" -o -name "spec*" -o -name "api" -o -name "auth" -o -name "security" \) -print 2>/dev/null || echo "No specialized directories found"`
- Config patterns: !`find . -path ./node_modules -prune -o \( -name "*.config.*" -o -name "*rc.*" -o -name ".*rc" \) -print 2>/dev/null || echo "No config files found"`

## Task: Create Custom Steering Document

You will create a new custom steering document based on user requirements. Common use cases include:

### Common Custom Steering Types

1. **API Standards** (`api-standards.md`)
   - REST/GraphQL conventions
   - Error handling patterns
   - Authentication/authorization approaches
   - Rate limiting and pagination
   - API versioning strategy

2. **Testing Approach** (`testing.md`)
   - Test file organization
   - Naming conventions for tests
   - Mocking strategies
   - Coverage requirements
   - E2E vs unit vs integration testing

3. **Code Style Guidelines** (`code-style.md`)
   - Language-specific conventions
   - Formatting rules beyond linters
   - Comment standards
   - Function/variable naming patterns
   - Code organization principles

4. **Security Policies** (`security.md`)
   - Input validation requirements
   - Authentication patterns
   - Secrets management
   - OWASP compliance guidelines
   - Security review checklist

5. **Database Conventions** (`database.md`)
   - Schema design patterns
   - Migration strategies
   - Query optimization guidelines
   - Connection pooling settings
   - Backup and recovery procedures

6. **Performance Standards** (`performance.md`)
   - Load time requirements
   - Memory usage limits
   - Optimization techniques
   - Caching strategies
   - Monitoring and profiling

7. **Deployment Workflow** (`deployment.md`)
   - CI/CD pipeline stages
   - Environment configurations
   - Release procedures
   - Rollback strategies
   - Health check requirements

## Inclusion Mode Selection

Choose the inclusion mode based on how frequently and in what context this steering document should be referenced:

### 1. Always Included (Use sparingly for custom files)
- **When to use**: Universal standards that apply to ALL code (security policies, core conventions)
- **Impact**: Increases context size for every interaction
- **Example**: `security-standards.md` for critical security requirements
- **Recommendation**: Only use for truly universal guidelines

### 2. Conditional Inclusion (Recommended for most custom files)  
- **When to use**: Domain-specific guidelines for particular file types or directories
- **File patterns**: `"*.test.js"`, `"src/api/**/*"`, `"**/auth/*"`, `"*.config.*"`
- **Example**: `testing-approach.md` only loads when editing test files
- **Benefits**: Relevant context without overwhelming general interactions

### 3. Manual Inclusion (Best for specialized contexts)
- **When to use**: Specialized knowledge needed occasionally 
- **Usage**: Reference with `@filename.md` during specific conversations
- **Example**: `deployment-runbook.md` for deployment-specific tasks
- **Benefits**: Available when needed, doesn't clutter routine interactions

## Document Structure Guidelines

Create the custom steering document with:

1. **Clear Title and Purpose**
   - What aspect of the project this document covers
   - When this guidance should be applied

2. **Specific Guidelines**
   - Concrete rules and patterns to follow
   - Rationale for important decisions

3. **Code Examples**
   - Show correct implementation patterns
   - Include counter-examples if helpful

4. **Integration Points**
   - How this relates to other steering documents
   - Dependencies or prerequisites

## Security and Quality Guidelines

### Security Requirements
- **Never include sensitive data**: No API keys, passwords, database URLs, secrets
- **Review sensitive context**: Avoid internal server names, private API endpoints
- **Team access awareness**: All steering content is shared with team members

### Content Quality Standards
- **Single responsibility**: One steering file = one domain (don't mix API + database guidelines)
- **Concrete examples**: Include code snippets and real project examples  
- **Clear rationale**: Explain WHY certain approaches are preferred
- **Maintainable size**: Target 2-3 minute read time per file

## Instructions

1. **Ask the user** for:
   - Document name (descriptive filename ending in .md)
   - Topic/purpose of the custom steering
   - Inclusion mode preference
   - Specific patterns for conditional inclusion (if applicable)

2. **Create the document** in `.kiro/steering/` with:
   - Clear, focused content (2-3 minute read)
   - Practical examples
   - Consistent formatting with other steering files

3. **Document the inclusion mode** by adding a comment at the top:
   ```markdown
   <!-- Inclusion Mode: Always | Conditional: "pattern" | Manual -->
   ```

4. **Validate** that the document:
   - Doesn't duplicate existing steering content
   - Provides unique value for the specified context
   - Follows markdown best practices

Remember: Custom steering documents should supplement, not replace, the foundational three files. They provide specialized context for specific aspects of your project.
ultrathink