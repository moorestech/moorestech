---
description: Analyze implementation gap between requirements and existing codebase
allowed-tools: Bash, Glob, Grep, Read, Write, Edit, MultiEdit, WebSearch, WebFetch
argument-hint: <feature-name>
---

# Implementation Gap Validation

Analyze implementation requirements and existing codebase for feature: **$1**

## Context Validation

### Steering Context
- Architecture context: @.kiro/steering/structure.md
- Technical constraints: @.kiro/steering/tech.md
- Product context: @.kiro/steering/product.md
- Custom steering: Load all "Always" mode custom steering files from .kiro/steering/

### Existing Spec Context
- Current spec directory: !`ls -la .kiro/specs/$1/`
- Requirements document: @.kiro/specs/$1/requirements.md
- Spec metadata: @.kiro/specs/$1/spec.json

## Task: Implementation Gap Analysis

### Prerequisites
- Requirements document must exist: `.kiro/specs/$1/requirements.md`
- If not exist, stop with message: "Run `/kiro:spec-requirements $1` first to generate requirements"

### Analysis Process

#### 1. Current State Investigation
**Existing Codebase Analysis**:
- Identify files and modules related to the feature domain
- Map current architecture patterns, conventions, and tech stack usage
- Document existing services, utilities, and reusable components
- Understand current data models, APIs, and integration patterns

**Code Structure Assessment**:
- Document file organization, naming conventions, and architectural layers
- Extract import/export patterns and module dependency structures  
- Identify existing testing patterns (file placement, frameworks, mocking approaches)
- Map API client, database, and authentication implementation approaches currently used
- Note established coding standards and development practices

#### 2. Requirements Feasibility Analysis
**Technical Requirements Extraction**:
- Parse EARS format requirements from requirements.md
- Identify technical components needed for each requirement
- Extract non-functional requirements (security, performance, etc.)
- Map business logic complexity and integration points

**Gap Identification**:
- Missing technical capabilities vs requirements
- Unknown technologies or external dependencies
- Potential integration challenges with existing systems
- Areas requiring research or proof-of-concept work

#### 3. Implementation Approach Options
**Multiple Strategy Evaluation**:
- **Option A**: Extend existing components/files
  - Which existing files/modules to extend
  - Compatibility with current patterns
  - Code complexity and maintainability impact

- **Option B**: Create new components (when justified)
  - Clear rationale for new file creation
  - Integration points with existing system
  - Responsibility boundaries and interfaces

- **Option C**: Hybrid approach
  - Combination of extension and new creation
  - Phased implementation strategy
  - Risk mitigation approach

#### 4. Technical Research Requirements
**External Dependencies Analysis** (if any):
- Required libraries, APIs, or services not currently used
- Version compatibility with existing dependencies
- Authentication, configuration, and setup requirements
- Rate limits, usage constraints, and cost implications

**Knowledge Gap Assessment**:
- Technologies unfamiliar to the team
- Complex integration patterns requiring research
- Performance or security considerations needing investigation
- Best practice research requirements

#### 5. Implementation Complexity Assessment
**Effort Estimation**:
- **Small (S)**: 1-3 days, mostly using existing patterns
- **Medium (M)**: 3-7 days, some new patterns or integrations
- **Large (L)**: 1-2 weeks, significant new functionality
- **Extra Large (XL)**: 2+ weeks, complex architecture changes

**Risk Factors**:
- High: Unknown technologies, complex integrations, architectural changes
- Medium: New patterns, external dependencies, performance requirements
- Low: Extending existing patterns, well-understood technologies

### Output Format

Generate analysis in the language specified in spec.json (check `.kiro/specs/$1/spec.json` for "language" field):

#### Analysis Summary
- Feature scope and complexity overview
- Key technical challenges identified
- Overall implementation approach recommendation

#### Existing Codebase Insights
- Relevant existing components and their current responsibilities
- Established patterns and conventions to follow
- Reusable utilities and services available

#### Implementation Strategy Options
For each viable approach:
- **Approach**: [Extension/New/Hybrid]
- **Rationale**: Why this approach makes sense
- **Trade-offs**: Pros and cons of this approach
- **Complexity**: [S/M/L/XL] with reasoning

#### Technical Research Needs
- External dependencies requiring investigation
- Unknown technologies needing research
- Integration patterns requiring proof-of-concept
- Performance or security considerations to investigate

#### Recommendations for Design Phase
- Preferred implementation approach with rationale
- Key architectural decisions that need to be made
- Areas requiring further investigation during design
- Potential risks to address in design phase

## Instructions

1. **Check spec.json for language** - Use the language specified in the metadata
2. **Prerequisites validation** - Ensure requirements are approved
3. **Thorough investigation** - Analyze existing codebase comprehensively
4. **Multiple options** - Present viable implementation approaches
5. **Information focus** - Provide analysis, not final decisions
6. **Research identification** - Flag areas needing investigation
7. **Design preparation** - Set up design phase for success

**CRITICAL**: This is an analysis phase. Provide information and options, not final implementation decisions. The design phase will make strategic choices based on this analysis.

---

## Next Phase: Design Generation

After validation, proceed to design phase:

**Generate design based on analysis:**
Run `/kiro:spec-design $1` to create technical design document

**Auto-approve and proceed:**  
Run `/kiro:spec-design $1 -y` to auto-approve requirements and generate design directly