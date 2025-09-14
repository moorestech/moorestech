---
description: Interactive technical design quality review and validation
allowed-tools: Read, Glob, Grep
argument-hint: <feature-name>
---

# Technical Design Validation

Interactive design quality review for feature: **$1**

## Context Loading

### Prerequisites Validation
- Design document must exist: `.kiro/specs/$1/design.md`
- If not exist, stop with message: "Run `/kiro:spec-design $1` first to generate design document"

### Review Context
- Spec metadata: @.kiro/specs/$1/spec.json
- Requirements document: @.kiro/specs/$1/requirements.md
- Design document: @.kiro/specs/$1/design.md
- Core steering documents:
  - Architecture: @.kiro/steering/structure.md
  - Technology: @.kiro/steering/tech.md
  - Product context: @.kiro/steering/product.md
- Custom steering: All additional `.md` files in `.kiro/steering/` directory

## Task: Interactive Design Quality Review

### Review Methodology

**Focus**: Critical issues only - limit to 3 most important concerns
**Format**: Interactive dialogue with immediate feedback and improvement suggestions
**Outcome**: GO/NO-GO decision with clear rationale

### Core Review Criteria

#### 1. Existing Architecture Alignment (Critical)
**Evaluation Points**:
- Integration with existing system boundaries and layers
- Consistency with established architectural patterns
- Proper dependency direction and coupling management
- Alignment with current module organization and responsibilities

**Review Questions**:
- Does this design respect existing architectural boundaries?
- Are new components properly integrated with existing systems?
- Does the design follow established patterns and conventions?

#### 2. Design Consistency & Standards
**Evaluation Points**:
- Adherence to project naming conventions and code standards
- Consistent error handling and logging strategies
- Uniform approach to configuration and dependency management
- Alignment with established data modeling patterns

**Review Questions**:
- Is the design consistent with existing code standards?
- Are error handling and configuration approaches unified?
- Does naming and structure follow project conventions?

#### 3. Extensibility & Maintainability
**Evaluation Points**:
- Design flexibility for future requirements changes
- Clear separation of concerns and single responsibility principle
- Testability and debugging considerations
- Documentation and code clarity requirements

**Review Questions**:
- How well does this design handle future changes?
- Are responsibilities clearly separated and testable?
- Is the design complexity appropriate for the requirements?

#### 4. Type Safety & Interface Design
**Evaluation Points** (for TypeScript projects):
- Proper type definitions and interface contracts
- Avoidance of `any` types and unsafe patterns
- Clear API boundaries and data structure definitions
- Input validation and error handling coverage

**Review Questions**:
- Are types properly defined and interfaces clear?
- Is the API design robust and well-defined?
- Are edge cases and error conditions handled appropriately?

### Interactive Review Process

#### Step 1: Design Analysis
Thoroughly analyze the design document against all review criteria, identifying the most critical issues that could impact:
- System integration and compatibility
- Long-term maintainability
- Implementation complexity and risks
- Requirements fulfillment accuracy

#### Step 2: Critical Issues Identification
**Limit to 3 most important concerns maximum**. For each critical issue:

**Issue Format**:
```
🔴 **Critical Issue [1-3]**: [Brief title]
**Concern**: [Specific problem description]
**Impact**: [Why this matters for the project]
**Suggestion**: [Concrete improvement recommendation]
```

#### Step 3: Design Strengths Recognition
Acknowledge 1-2 strong aspects of the design to maintain balanced feedback.

#### Step 4: GO/NO-GO Decision

**GO Criteria**:
- No critical architectural misalignment
- Requirements adequately addressed
- Implementation path is clear and reasonable
- Risks are acceptable and manageable

**NO-GO Criteria**:
- Fundamental architectural conflicts
- Critical requirements not addressed
- Implementation approach has high failure risk
- Design complexity disproportionate to requirements

### Output Format

Generate review in the language specified in spec.json (check `.kiro/specs/$1/spec.json` for "language" field):

#### Design Review Summary
Brief overview of the design's overall quality and readiness.

#### Critical Issues (Maximum 3)
For each issue identified:
- **Issue**: Clear problem statement
- **Impact**: Why it matters
- **Recommendation**: Specific improvement suggestion

#### Design Strengths
1-2 positive aspects worth highlighting.

#### Final Assessment
**Decision**: GO / NO-GO
**Rationale**: Clear reasoning for the decision
**Next Steps**: What should happen next

#### Interactive Discussion
Engage in dialogue about:
- Designer's perspective on identified issues
- Alternative approaches or trade-offs
- Clarification of design decisions
- Agreement on necessary changes (if any)

## Review Guidelines

1. **Critical Focus**: Only flag issues that significantly impact success
2. **Constructive Tone**: Provide solutions, not just criticism
3. **Interactive Approach**: Engage in dialogue rather than one-way evaluation
4. **Balanced Assessment**: Recognize both strengths and weaknesses
5. **Clear Decision**: Make definitive GO/NO-GO recommendation
6. **Actionable Feedback**: Ensure all suggestions are implementable

## Instructions

1. **Load all context documents** - Understand full project scope
2. **Analyze design thoroughly** - Review against all criteria
3. **Identify critical issues only** - Focus on most important problems
4. **Engage interactively** - Discuss findings with user
5. **Make clear decision** - Provide definitive GO/NO-GO
6. **Guide next steps** - Clear direction for proceeding

**Remember**: This is quality assurance, not perfection seeking. The goal is ensuring the design is solid enough to proceed to implementation with acceptable risk.

---

## Next Phase: Task Generation

After design validation:

**If design passes validation (GO decision):**
Run `/kiro:spec-tasks $1` to generate implementation tasks

**Auto-approve and proceed:**
Run `/kiro:spec-tasks $1 -y` to auto-approve requirements and design, then generate tasks directly