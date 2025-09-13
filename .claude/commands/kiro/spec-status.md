---
description: Show specification status and progress
allowed-tools: Bash, Read, Glob, Write, Edit, MultiEdit, Update
---

# Specification Status

Show current status and progress for feature: **$ARGUMENTS**

## Spec Context

### Spec Files
- Spec directory: !`ls -la .kiro/specs/$ARGUMENTS/`
- Spec metadata: @.kiro/specs/$ARGUMENTS/spec.json
- Requirements: @.kiro/specs/$ARGUMENTS/requirements.md
- Design: @.kiro/specs/$ARGUMENTS/design.md
- Tasks: @.kiro/specs/$ARGUMENTS/tasks.md

### All Specs Overview
- Available specs: !`ls -la .kiro/specs/`
- Active specs: !`find .kiro/specs/ -name "spec.json" -exec grep -l "implementation_ready.*true" {} \;`

## Task: Generate Status Report

Create comprehensive status report for the specification in the language specified in spec.json (check `@.kiro/specs/$ARGUMENTS/spec.json` for "language" field):

### 1. Specification Overview
Display:
- Feature name and description
- Creation date and last update
- Current phase (requirements/design/tasks/implementation)
- Overall completion percentage

### 2. Phase Status
For each phase, show:
- ✅ **Requirements Phase**: [completion %]
  - Requirements count: [number]
  - Acceptance criteria defined: [yes/no]
  - Requirements coverage: [complete/partial/missing]

- ✅ **Design Phase**: [completion %]
  - Architecture documented: [yes/no]
  - Components defined: [yes/no]
  - Diagrams created: [yes/no]
  - Integration planned: [yes/no]

- ✅ **Tasks Phase**: [completion %]
  - Total tasks: [number]
  - Completed tasks: [number]
  - Remaining tasks: [number]
  - Blocked tasks: [number]

### 3. Implementation Progress
If in implementation phase:
- Task completion breakdown
- Current blockers or issues
- Estimated time to completion
- Next actions needed

#### Task Completion Tracking
- Parse tasks.md checkbox status: `- [x]` (completed) vs `- [ ]` (pending)
- Count completed vs total tasks
- Show completion percentage
- Identify next uncompleted task

### 4. Quality Metrics
Show:
- Requirements coverage: [percentage]
- Design completeness: [percentage]
- Task granularity: [appropriate/too large/too small]
- Dependencies resolved: [yes/no]

### 5. Recommendations
Based on status, provide:
- Next steps to take
- Potential issues to address
- Suggested improvements
- Missing elements to complete

### 6. Steering Alignment
Check alignment with steering documents:
- Architecture consistency: [aligned/misaligned]
- Technology stack compliance: [compliant/non-compliant]
- Product requirements alignment: [aligned/misaligned]

## Instructions

1. **Check spec.json for language** - Use the language specified in the metadata
2. **Parse all spec files** to understand current state
3. **Calculate completion percentages** for each phase
4. **Identify next actions** based on current progress
5. **Highlight any blockers** or issues
6. **Provide clear recommendations** for moving forward
7. **Check steering alignment** to ensure consistency

Generate status report that provides clear visibility into spec progress and next steps.