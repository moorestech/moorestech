---
description: Initialize a new specification with detailed project description and requirements
allowed-tools: Bash, Read, Write, Glob
model: claude-sonnet-4-20250514
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
Create `.kiro/specs/{generated-feature-name}/` directory with template files:
- `requirements.md` - Empty template for user stories
- `design.md` - Empty template for technical design  
- `tasks.md` - Empty template for implementation tasks
- `spec.json` - Metadata and approval tracking

### 3. Initialize spec.json Metadata
Create initial metadata with approval tracking:
```json
{
  "feature_name": "{generated-feature-name}",
  "created_at": "current_timestamp",
  "updated_at": "current_timestamp",
  "language": "japanese",
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

### 4. Create Template Files with Project Context

#### requirements.md (Template)
```markdown
# Requirements Document

## Overview
<!-- Project overview will be generated in /kiro:spec-requirements phase -->

## Project Description (User Input)
$ARGUMENTS

## Requirements
<!-- Detailed user stories will be generated in /kiro:spec-requirements phase -->

```

#### design.md (Empty Template)
```markdown
# Design Document

## Overview
<!-- Technical design will be generated after requirements approval -->

```

#### tasks.md (Empty Template)
```markdown
# Implementation Plan

<!-- Implementation tasks will be generated after design approval -->

```

### 5. Update CLAUDE.md Reference
Add the new spec to the active specifications list with the generated feature name and a brief description.

## Next Steps After Initialization

Follow the spec-driven development workflow:
1. `/kiro:spec-requirements {feature-name}` - Generate requirements
2. `/kiro:spec-design {feature-name}` - Generate design (interactive approval)
3. `/kiro:spec-tasks {feature-name}` - Generate tasks (interactive approval)

## Output Format

After initialization, provide:
1. Generated feature name and rationale
2. Brief project summary
3. Created file paths
4. Next command: `/kiro:spec-requirements {feature-name}`