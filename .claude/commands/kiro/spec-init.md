---
description: Initialize a new specification with detailed project description and requirements
allowed-tools: Bash, Read, Write, Glob
argument-hint: <project-description>
---

# Spec Initialization

Initialize a new specification based on the provided project description:

**Project Description**: $ARGUMENTS

## Task: Initialize Specification Structure

**SCOPE**: This command initializes the directory structure and metadata based on the detailed project description provided.

### 1. Generate Feature Name
Create a concise, descriptive feature name from the project description ($ARGUMENTS).
**Check existing `.kiro/specs/` directory to ensure the generated feature name is unique. If a conflict exists, append a number suffix (e.g., feature-name-2).**

### 2. Create Spec Directory
Create `.kiro/specs/[generated-feature-name]/` directory with:
- `spec.json` - Metadata and approval tracking
- `requirements.md` - Lightweight template with project description

**Note**: design.md and tasks.md will be created by their respective commands during the development process.

### 3. Initialize spec.json Metadata
Create initial metadata with approval tracking:
```json
{
  "feature_name": "[generated-feature-name]",
  "created_at": "current_timestamp",
  "updated_at": "current_timestamp",
  "language": "ja",
  "phase": "initialized",
  "approvals": {
    "requirements": {
      "generated": false,
      "approved": false
    },
    "design": {
      "generated": false,
      "approved": false
    },
    "tasks": {
      "generated": false,
      "approved": false
    }
  },
  "ready_for_implementation": false
}
```

### 4. Create Requirements Template
Create requirements.md with project description:
```markdown
# Requirements Document

## Project Description (Input)
$ARGUMENTS

## Requirements
<!-- Will be generated in /kiro:spec-requirements phase -->
```

### 5. Update CLAUDE.md Reference
Add the new spec to the active specifications list with the generated feature name and a brief description.

## Next Steps After Initialization

Follow the strict spec-driven development workflow:
1. **`/kiro:spec-requirements <feature-name>`** - Create and generate requirements.md
2. **`/kiro:spec-design <feature-name>`** - Create and generate design.md (requires approved requirements)
3. **`/kiro:spec-tasks <feature-name>`** - Create and generate tasks.md (requires approved design)

**Important**: Each phase creates its respective file and requires approval before proceeding to the next phase.

## Output Format

After initialization, provide:
1. Generated feature name and rationale
2. Brief project summary
3. Created spec.json path
4. **Clear next step**: `/kiro:spec-requirements <feature-name>`
5. Explanation that only spec.json was created, following stage-by-stage development principles
