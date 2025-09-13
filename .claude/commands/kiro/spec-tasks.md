---
description: Generate implementation tasks for a specification
allowed-tools: Bash, Read, Write, Edit, Update, MultiEdit
argument-hint: <feature-name> [-y]
---

# Implementation Tasks

Generate detailed implementation tasks for feature: **$ARGUMENTS**

## Requirements & Design Approval Required

**CRITICAL**: Tasks can only be generated after both requirements and design are reviewed and approved.

- Requirements document: @.kiro/specs/$ARGUMENTS/requirements.md
- Design document: @.kiro/specs/$ARGUMENTS/design.md
- Spec metadata: @.kiro/specs/$ARGUMENTS/spec.json

**Note**: If this command was called with `-y` flag, both requirements and design are auto-approved (spec.json updated to set requirements.approved=true and design.approved=true). Otherwise, both phases must be approved first via their respective commands followed by `/kiro:spec-tasks $ARGUMENTS -y`.

## Context Analysis

### Complete Spec Context (APPROVED)
- Requirements: @.kiro/specs/$ARGUMENTS/requirements.md
- Design: @.kiro/specs/$ARGUMENTS/design.md
- Current tasks: @.kiro/specs/$ARGUMENTS/tasks.md
- Spec metadata: @.kiro/specs/$ARGUMENTS/spec.json

### Steering Context
- Architecture patterns: @.kiro/steering/structure.md
- Development practices: @.kiro/steering/tech.md
- Product constraints: @.kiro/steering/product.md
- Custom steering: Load "Always" mode and task-related "Conditional" mode files

## Task: Generate Code-Generation Prompts

**Prerequisites Verified**: Both requirements and design are approved and ready for task breakdown.

**CRITICAL**: Convert the feature design into a series of prompts for a code-generation LLM that will implement each step in a test-driven manner. Prioritize best practices, incremental progress, and early testing, ensuring no big jumps in complexity at any stage.

Create implementation plan in the language specified in spec.json:

### 1. Code-Generation Tasks Structure
Create tasks.md in the language specified in spec.json (check `@.kiro/specs/$ARGUMENTS/spec.json` for "language" field):

**Note**: The example below is for format reference only. Actual content should be based on the specific requirements and design documents for your project.

```markdown
# Implementation Plan

- [ ] 1. Set up project structure and core configuration
  - Create Node.js project with TypeScript configuration
  - Set up Express server with basic middleware
  - Configure PostgreSQL and Redis connections
  - Set up environment configuration and secrets management
  - _Requirements: All requirements need foundational setup_

- [ ] 2. Implement authentication and user management
- [ ] 2.1 Create user authentication system
  - Implement User model with validation
  - Create JWT token generation and validation utilities
  - Build user registration and login endpoints
  - Write unit tests for authentication logic
  - _Requirements: 7.1, 7.2_

- [ ] 2.2 Implement email account connection system
  - Create EmailAccount model with encrypted credential storage
  - Implement OAuth 2.0 flow for Gmail and Outlook
  - Build IMAP/SMTP credential validation
  - Create email account management endpoints
  - Write tests for account connection flows
  - _Requirements: 5.1, 5.2, 5.4_

... (Continue with additional phases: API layer, frontend, integration testing, etc.)
```

**Task Structure Requirements:**
- Use section headers to group related functionality (## [Functional Area])
- Use flat numbering within sections: Major tasks (1, 2, 3) and sub-tasks (1.1, 1.2)
- Avoid "Phase X:" prefixes, use functional section names instead
- Each task should have 3-5 sub-items maximum
- Keep tasks completable in 1-2 hours
- Order by technical dependencies: Each task should build on outputs from previous tasks
- Each task explains how it connects to subsequent tasks
- **MUST end with exact format**: _Requirements: X.X, Y.Y_ or _Requirements: [description]_ (underscores mandatory)
- Rely on design document for implementation details

### 2. Focus on Coding Activities Only
**INCLUDE:** Any task that involves writing, modifying, or testing code
**EXCLUDE:** User testing, deployment, metrics gathering, CI/CD setup, documentation creation

### 3. Granular Requirements Mapping
**MANDATORY FORMAT**: Each task must end with _Requirements: [mapping]_
- **Primary format**: _Requirements: 2.1, 3.3, 1.2_ for specific EARS requirements (most common)
- **Generalized mapping**: _Requirements: All requirements need foundational setup_ for cross-cutting tasks
- **End-to-end tasks**: _Requirements: All requirements need E2E validation_ for comprehensive testing
- Must use exact format with underscores
- Ensure every EARS requirement is covered by implementation tasks


### 4. Document Generation Only
Generate the tasks document content ONLY. Do not include any review or approval instructions in the actual document file.

### 5. Update Metadata

Update spec.json with:
```json
{
  "phase": "tasks-generated",
  "approvals": {
    "requirements": {
      "generated": true,
      "approved": true
    },
    "design": {
      "generated": true,
      "approved": true
    },
    "tasks": {
      "generated": true,
      "approved": false
    }
  },
  "updated_at": "current_timestamp"
}
```

### 8. Metadata Update
Update the tracking metadata to reflect task generation completion.

---

## INTERACTIVE APPROVAL IMPLEMENTED (Not included in document)

The following is for Claude Code conversation only - NOT for the generated document:

## Next Phase: Implementation Ready

After generating tasks.md, review the implementation tasks:

**If tasks look good:**
Begin implementation following the generated task sequence

**If tasks need modification:**
Request changes and re-run this command after modifications

Tasks represent the final planning phase - implementation can begin once tasks are approved.

**Final approval process for implementation**:
```
📋 Tasks review completed. Ready for implementation.
📄 Generated: .kiro/specs/feature-name/tasks.md
✅ All phases approved. Implementation can now begin.
```

### Review Checklist (for user reference):
- [ ] Tasks are properly sized (2-4 hours each)
- [ ] All requirements are covered by tasks
- [ ] Task dependencies are correct
- [ ] Technology choices match the design
- [ ] Testing tasks are included

## Instructions

### Core Task Generation
1. **Check spec.json for language** - Use the language specified in the metadata
2. **Convert design into code-generation prompts** - Each task must be a specific coding instruction
3. **Assume context availability** - All context documents (requirements.md, design.md) will be available during implementation
4. **Specify exact files and components** - Define what code to write/modify in which files
5. **Build incrementally** - Each task uses outputs from previous tasks, no orphaned code
6. **Map to requirements** - End each task with _Requirements: X.X, Y.Y_ or _Requirements: [description]_ format

### Implementation Strategy
7. **Prioritize early validation** - Sequence steps to validate core functionality early through code
8. **Apply test-driven approach** - Integrate testing into each development task
9. **Order by technical dependencies** - Ensure each task can execute using outputs from previous tasks
10. **Size appropriately** - Each task should be completable in 1-3 hours

### Strict Coding-Only Focus
**INCLUDE only tasks involving:**
- Writing, modifying, or testing code
- Creating automated tests
- Implementing specific functions or components

**EXCLUDE all non-coding activities:**
- User acceptance testing or feedback gathering
- Deployment to production/staging environments
- Performance metrics gathering or analysis
- Running the application manually for end-to-end testing
- User training or documentation creation
- Business process or organizational changes
- Marketing or communication activities
- Any task that cannot be completed through code modification

### Completion
11. **Update tracking metadata** upon completion - Set phase to "tasks-generated"

Generate step-by-step implementation tasks executable by a coding agent.
think deeply