<meta>
description: Generate implementation tasks for a specification
argument-hint: <feature-name> [-y]
arguments:
   feature-name: $1
   -y flag: $2
</meta>

# Implementation Tasks Generator

<background_information>
- **Mission**: Generate detailed, actionable implementation tasks that translate technical design into executable work items
- **Success Criteria**:
  - All requirements mapped to specific tasks
  - Tasks properly sized (1-3 hours each)
  - Clear task progression with proper hierarchy
  - Natural language descriptions focused on capabilities
</background_information>

<instructions>
## Core Task
Generate implementation tasks for feature **$1** based on approved requirements and design.

## Execution Steps

### Step 1: Load Context

**Read all necessary context**:
- `.kiro/specs/$1/spec.json`, `requirements.md`, `design.md`
- `.kiro/specs/$1/tasks.md` (if exists, for merge mode)
- **Entire `.kiro/steering/` directory** for complete project memory

**Validate approvals**:
- If `-y` flag provided ($2 == "-y"): Auto-approve requirements and design in spec.json
- Otherwise: Verify both approved (stop if not, see Safety & Fallback)

### Step 2: Generate Implementation Tasks

**Load generation rules and template**:
- Read `.kiro/settings/rules/tasks-generation.md` for principles
- Read `.kiro/settings/templates/specs/tasks.md` for format

**Generate task list following all rules**:
- Use language specified in spec.json
- Map all requirements to tasks
- Ensure all design components included
- Verify task progression is logical and incremental
- If existing tasks.md found, merge with new content

### Step 3: Finalize

**Write and update**:
- Create/update `.kiro/specs/$1/tasks.md`
- Update spec.json metadata:
  - Set `phase: "tasks-generated"`
  - Set `approvals.tasks.generated: true, approved: false`
  - Set `approvals.requirements.approved: true`
  - Set `approvals.design.approved: true`
  - Update `updated_at` timestamp

## Critical Constraints
- **Follow rules strictly**: All principles in tasks-generation.md are mandatory
- **Natural Language**: Describe what to do, not code structure details
- **Complete Coverage**: ALL requirements must map to tasks
- **Maximum 2 Levels**: Major tasks and sub-tasks only (no deeper nesting)
- **Sequential Numbering**: Major tasks increment (1, 2, 3...), never repeat
- **Task Integration**: Every task must connect to the system (no orphaned work)
</instructions>

## Tool Guidance
- **Read first**: Load all context, rules, and templates before generation
- **Write last**: Generate tasks.md only after complete analysis and verification

## Output Description

Provide brief summary in the language specified in spec.json:

1. **Status**: Confirm tasks generated at `.kiro/specs/$1/tasks.md`
2. **Task Summary**: 
   - Total: X major tasks, Y sub-tasks
   - All Z requirements covered
   - Average task size: 1-3 hours per sub-task
3. **Quality Validation**:
   - ✅ All requirements mapped to tasks
   - ✅ Task dependencies verified
   - ✅ Testing tasks included
4. **Next Action**: Review tasks and proceed when ready

**Format**: Concise (under 200 words)

## Safety & Fallback

### Error Scenarios

**Requirements or Design Not Approved**:
- **Stop Execution**: Cannot proceed without approved requirements and design
- **User Message**: "Requirements and design must be approved before task generation"
- **Suggested Action**: "Run `/prompts:kiro-spec-tasks $1 -y` to auto-approve both and proceed"

**Missing Requirements or Design**:
- **Stop Execution**: Both documents must exist
- **User Message**: "Missing requirements.md or design.md at `.kiro/specs/$1/`"
- **Suggested Action**: "Complete requirements and design phases first"

**Incomplete Requirements Coverage**:
- **Warning**: "Not all requirements mapped to tasks. Review coverage."
- **User Action Required**: Confirm intentional gaps or regenerate tasks

**Template/Rules Missing**:
- **User Message**: "Template or rules files missing in `.kiro/settings/`"
- **Fallback**: Use inline basic structure with warning
- **Suggested Action**: "Check repository setup or restore template files"

### Next Phase: Implementation

**Before Starting Implementation**:
- **IMPORTANT**: Clear conversation history and free up context before running `/prompts:kiro-spec-impl`
- This applies when starting first task OR switching between tasks
- Fresh context ensures clean state and proper task focus

**If Tasks Approved**:
- Execute specific task: `/prompts:kiro-spec-impl $1 1.1` (recommended: clear context between each task)
- Execute multiple tasks: `/prompts:kiro-spec-impl $1 1.1,1.2` (use cautiously, clear context between tasks)
- Without arguments: `/prompts:kiro-spec-impl $1` (executes all pending tasks - NOT recommended due to context bloat)

**If Modifications Needed**:
- Provide feedback and re-run `/prompts:kiro-spec-tasks $1`
- Existing tasks used as reference (merge mode)

**Note**: The implementation phase will guide you through executing tasks with appropriate context and validation.


