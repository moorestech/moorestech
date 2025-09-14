---
description: Create comprehensive technical design for a specification  
allowed-tools: Bash, Glob, Grep, LS, Read, Write, Edit, MultiEdit, Update, WebSearch, WebFetch
argument-hint: <feature-name> [-y]
---

# Technical Design

Generate a **technical design document** for feature **$1**.

## Task: Create Technical Design Document

### 1. Prerequisites & File Handling
- **Requirements Approval Check**: 
  - If invoked with `-y` ($2 == "-y"), set `requirements.approved=true` in `spec.json`
  - Otherwise, **stop** with an actionable message if requirements are missing or unapproved
- **Design File Handling**:
  - If design.md does not exist: Create new design.md file
  - If design.md exists: Interactive prompt with options:
    - **[o] Overwrite**: Generate completely new design document
    - **[m] Merge**: Generate new design document using existing content as reference context  
    - **[c] Cancel**: Stop execution for manual review
- **Context Loading**: Read `.kiro/specs/$1/requirements.md`, core steering documents, and existing design.md (if merge mode)

### 2. Discovery & Analysis Phase

**CRITICAL**: Before generating the design, conduct thorough research and analysis:

#### Feature Classification & Process Adaptation
**Classify feature type to adapt process scope**:
- **New Feature** (greenfield): Full process including technology selection and architecture decisions
- **Extension** (existing system): Focus on integration analysis, minimal architectural changes
- **Simple Addition** (CRUD, UI): Streamlined process, follow established patterns
- **Complex Integration** (external systems, new domains): Comprehensive analysis and risk assessment

**Process Adaptation**: Skip or streamline analysis steps based on classification above

#### A. Requirements to Technical Components Mapping
- Map requirements (EARS format) to technical components
- Extract non-functional requirements (performance, security, scalability)
- Identify core technical challenges and constraints

#### B. Existing Implementation Analysis 
**MANDATORY when modifying or extending existing features**:
- Analyze codebase structure, dependencies, patterns
- Map reusable modules, services, utilities
- Understand domain boundaries, layers, data flow
- Determine extension vs. refactor vs. wrap approach
- Prioritize minimal changes and file reuse

**Optional for completely new features**: Review existing patterns for consistency and reuse opportunities

#### C. Steering Alignment Check
- Verify alignment with core steering documents (`structure.md`, `tech.md`, `product.md`) and any custom steering files
  - **Core steering**: @.kiro/steering/structure.md, @.kiro/steering/tech.md, @.kiro/steering/product.md
  - **Custom steering**: All additional `.md` files in `.kiro/steering/` directory (e.g., `api.md`, `testing.md`, `security.md`)
- Document deviations with rationale for steering updates

#### D. Technology & Alternative Analysis
**For New Features or Unknown Technology Areas**:
- Research latest best practices using WebSearch/WebFetch when needed in parallel
- Compare relevant architecture patterns (MVC, Clean, Hexagonal) if pattern selection is required
- Assess technology stack alternatives only when technology choices are being made
- Document key findings that impact design decisions

**Skip this step if**: Using established team technology stack and patterns for straightforward feature additions

#### E. Implementation-Specific Investigation
**When new technology or complex integration is involved**:
- Verify specific API capabilities needed for requirements
- Check version compatibility with existing dependencies
- Identify configuration and setup requirements
- Document any migration or integration challenges

**For ANY external dependencies (libraries, APIs, services)**:
- Use WebSearch to find official documentation and community resources
- Use WebFetch to analyze specific documentation pages
- Document authentication flows, rate limits, and usage constraints
- Note any gaps in understanding for implementation phase

**Skip only if**: Using well-established internal libraries with no external dependencies

#### F. Technical Risk Assessment
- Performance/scalability risks: bottlenecks, capacity, growth
- Security vulnerabilities: attack vectors, compliance gaps
- Maintainability risks: complexity, knowledge, support
- Integration complexity: dependencies, coupling, API changes
- Technical debt: new creation vs. existing resolution

## Design Document Structure & Guidelines

### Core Principles
- **Review-optimized structure**: Critical technical decisions prominently placed to prevent oversight
- **Contextual relevance**: Include sections only when applicable to project type and scope
- **Visual-first design**: Essential Mermaid diagrams for architecture and data flow
- **Design focus only**: Architecture and interfaces, NO implementation code
- **Type safety**: Never use `any` type - define explicit types and interfaces
- **Formal tone**: Use definitive, declarative statements without hedging language
- **Language**: Use language from `spec.json.language` field, default to English

### Document Sections

**CORE SECTIONS** (Include when relevant):
- Overview, Architecture, Components and Interfaces (always)
- Data Models, Error Handling, Testing Strategy (when applicable)
- Security Considerations (when security implications exist)

**CONDITIONAL SECTIONS** (Include only when specifically relevant):
- Performance & Scalability (for performance-critical features)
- Migration Strategy (for existing system modifications)

<structured-document>
## Overview 
2-3 paragraphs max
**Purpose**: This feature delivers [specific value] to [target users].
**Users**: [Target user groups] will utilize this for [specific workflows].
**Impact** (if applicable): Changes the current [system state] by [specific modifications].


### Goals
- Primary objective 1
- Primary objective 2  
- Success criteria

### Non-Goals
- Explicitly excluded functionality
- Future considerations outside current scope
- Integration points deferred

## Architecture

### Existing Architecture Analysis (if applicable)
When modifying existing systems:
- Current architecture patterns and constraints
- Existing domain boundaries to be respected
- Integration points that must be maintained
- Technical debt addressed or worked around

### High-Level Architecture
**RECOMMENDED**: Include Mermaid diagram showing system architecture (required for complex features, optional for simple additions)

**Architecture Integration**:
- Existing patterns preserved: [list key patterns]
- New components rationale: [why each is needed]
- Technology alignment: [how it fits current stack]
- Steering compliance: [principles maintained]

### Technology Stack and Design Decisions

**Generation Instructions** (DO NOT include this section in design.md):
Adapt content based on feature classification from Discovery & Analysis Phase:

**For New Features (greenfield)**:
Generate Technology Stack section with ONLY relevant layers:
- Include only applicable technology layers (e.g., skip Frontend for CLI tools, skip Infrastructure for libraries)
- For each technology choice, provide: selection, rationale, and alternatives considered
- Include Architecture Pattern Selection if making architectural decisions

**For Extensions/Additions to Existing Systems**:
Generate Technology Alignment section instead:
- Document how feature aligns with existing technology stack
- Note any new dependencies or libraries being introduced
- Justify deviations from established patterns if necessary

**Key Design Decisions**:
Generate 1-3 critical technical decisions that significantly impact the implementation.
Each decision should follow this format:
- **Decision**: [Specific technical choice made]
- **Context**: [Problem or requirement driving this decision]
- **Alternatives**: [2-3 other approaches considered]
- **Selected Approach**: [What was chosen and how it works]
- **Rationale**: [Why this is optimal for the specific context]
- **Trade-offs**: [What we gain vs. what we sacrifice]

Skip this entire section for simple CRUD operations or when following established patterns without deviation.

## System Flows

**Flow Design Generation Instructions** (DO NOT include this section in design.md):
Generate appropriate flow diagrams ONLY when the feature requires flow visualization. Select from:
- **Sequence Diagrams**: For user interactions across multiple components
- **Process Flow Charts**: For complex algorithms, decision branches, or state machines  
- **Data Flow Diagrams**: For data transformations, ETL processes, or data pipelines
- **State Diagrams**: For complex state transitions
- **Event Flow**: For async/event-driven architectures

Skip this section entirely for simple CRUD operations or features without complex flows.
When included, provide concise Mermaid diagrams specific to the actual feature requirements.

## Requirements Traceability

**Traceability Generation Instructions** (DO NOT include this section in design.md):
Generate traceability mapping ONLY for complex features with multiple requirements or when explicitly needed for compliance/validation.

When included, create a mapping table showing how each EARS requirement is realized:
| Requirement | Requirement Summary | Components | Interfaces | Flows |
|---------------|-------------------|------------|------------|-------|
| 1.1 | Brief description | Component names | API/Methods | Relevant flow diagrams |

Alternative format for simpler cases:
- **1.1**: Realized by [Component X] through [Interface Y]
- **1.2**: Implemented in [Component Z] with [Flow diagram reference]

Skip this section for simple features with straightforward 1:1 requirement-to-component mappings.

## Components and Interfaces

**Component Design Generation Instructions** (DO NOT include this section in design.md):
Structure components by domain boundaries or architectural layers. Generate only relevant subsections based on component type.
Group related components under domain/layer headings for clarity.

### [Domain/Layer Name]

#### [Component Name]

**Responsibility & Boundaries**
- **Primary Responsibility**: Single, clear statement of what this component does
- **Domain Boundary**: Which domain/subdomain this belongs to
- **Data Ownership**: What data this component owns and manages
- **Transaction Boundary**: Scope of transactional consistency (if applicable)

**Dependencies**
- **Inbound**: Components/services that depend on this component
- **Outbound**: Components/services this component depends on
- **External**: Third-party services, libraries, or external systems

**External Dependencies Investigation** (when using external libraries/services):
- Use WebSearch to locate official documentation, GitHub repos, and community resources
- Use WebFetch to retrieve and analyze documentation pages, API references, and usage examples
- Verify API signatures, authentication methods, and rate limits
- Check version compatibility, breaking changes, and migration guides
- Investigate common issues, best practices, and performance considerations
- Document any assumptions, unknowns, or risks for implementation phase
- If critical information is missing, clearly note "Requires investigation during implementation: [specific concern]"

**Contract Definition**

Select and generate ONLY the relevant contract types for each component:

**Service Interface** (for business logic components):
```typescript
interface [ComponentName]Service {
  // Method signatures with clear input/output types
  // Include error types in return signatures
  methodName(input: InputType): Result<OutputType, ErrorType>;
}
```
- **Preconditions**: What must be true before calling
- **Postconditions**: What is guaranteed after successful execution
- **Invariants**: What remains true throughout

**API Contract** (for REST/GraphQL endpoints):
| Method | Endpoint | Request | Response | Errors |
|--------|----------|---------|----------|--------|
| POST | /api/resource | CreateRequest | Resource | 400, 409, 500 |

With detailed schemas only for complex payloads

**Event Contract** (for event-driven components):
- **Published Events**: Event name, schema, trigger conditions
- **Subscribed Events**: Event name, handling strategy, idempotency
- **Ordering**: Guaranteed order requirements
- **Delivery**: At-least-once, at-most-once, or exactly-once

**Batch/Job Contract** (for scheduled/triggered processes):
- **Trigger**: Schedule, event, or manual trigger conditions
- **Input**: Data source and validation rules
- **Output**: Results destination and format
- **Idempotency**: How repeat executions are handled
- **Recovery**: Failure handling and retry strategy

**State Management** (only if component maintains state):
- **State Model**: States and valid transitions
- **Persistence**: Storage strategy and consistency model
- **Concurrency**: Locking, optimistic/pessimistic control

**Integration Strategy** (when modifying existing systems):
- **Modification Approach**: Extend, wrap, or refactor existing code
- **Backward Compatibility**: What must be maintained
- **Migration Path**: How to transition from current to target state

## Data Models

**Data Model Generation Instructions** (DO NOT include this section in design.md):
Generate only relevant data model sections based on the system's data requirements and chosen architecture.
Progress from conceptual to physical as needed for implementation clarity.

### Domain Model
**When to include**: Complex business domains with rich behavior and rules

**Core Concepts**:
- **Aggregates**: Define transactional consistency boundaries
- **Entities**: Business objects with unique identity and lifecycle
- **Value Objects**: Immutable descriptive aspects without identity
- **Domain Events**: Significant state changes in the domain

**Business Rules & Invariants**:
- Constraints that must always be true
- Validation rules and their enforcement points
- Cross-aggregate consistency strategies

Include conceptual diagram (Mermaid) only when relationships are complex enough to benefit from visualization

### Logical Data Model
**When to include**: When designing data structures independent of storage technology

**Structure Definition**:
- Entity relationships and cardinality
- Attributes and their types
- Natural keys and identifiers
- Referential integrity rules

**Consistency & Integrity**:
- Transaction boundaries
- Cascading rules
- Temporal aspects (versioning, audit)

### Physical Data Model
**When to include**: When implementation requires specific storage design decisions

**For Relational Databases**:
- Table definitions with data types
- Primary/foreign keys and constraints
- Indexes and performance optimizations
- Partitioning strategy for scale

**For Document Stores**:
- Collection structures
- Embedding vs referencing decisions
- Sharding key design
- Index definitions

**For Event Stores**:
- Event schema definitions
- Stream aggregation strategies
- Snapshot policies
- Projection definitions

**For Key-Value/Wide-Column Stores**:
- Key design patterns
- Column families or value structures
- TTL and compaction strategies

### Data Contracts & Integration
**When to include**: Systems with service boundaries or external integrations

**API Data Transfer**:
- Request/response schemas
- Validation rules
- Serialization format (JSON, Protobuf, etc.)

**Event Schemas**:
- Published event structures
- Schema versioning strategy
- Backward/forward compatibility rules

**Cross-Service Data Management**:
- Distributed transaction patterns (Saga, 2PC)
- Data synchronization strategies
- Eventual consistency handling

Skip any section not directly relevant to the feature being designed.
Focus on aspects that influence implementation decisions.

## Error Handling

### Error Strategy
Concrete error handling patterns and recovery mechanisms for each error type.

### Error Categories and Responses
**User Errors** (4xx): Invalid input → field-level validation; Unauthorized → auth guidance; Not found → navigation help
**System Errors** (5xx): Infrastructure failures → graceful degradation; Timeouts → circuit breakers; Exhaustion → rate limiting  
**Business Logic Errors** (422): Rule violations → condition explanations; State conflicts → transition guidance

**Process Flow Visualization** (when complex business logic exists):
Include Mermaid flowchart only for complex error scenarios with business workflows.

### Monitoring
Error tracking, logging, and health monitoring implementation.

## Testing Strategy

### Default sections (adapt names/sections to fit the domain)
- Unit Tests: 3–5 items from core functions/modules (e.g., auth methods, subscription logic)
- Integration Tests: 3–5 cross-component flows (e.g., webhook handling, notifications)
- E2E/UI Tests (if applicable): 3–5 critical user paths (e.g., forms, dashboards)
- Performance/Load (if applicable): 3–4 items (e.g., concurrency, high-volume ops)

## Optional Sections (include when relevant)

### Security Considerations
**Include when**: Features handle authentication, sensitive data, external integrations, or user permissions
- Threat modeling, security controls, compliance requirements
- Authentication and authorization patterns
- Data protection and privacy considerations

### Performance & Scalability
**Include when**: Features have specific performance requirements, high load expectations, or scaling concerns
- Target metrics and measurement strategies
- Scaling approaches (horizontal/vertical)
- Caching strategies and optimization techniques

### Migration Strategy
**REQUIRED**: Include Mermaid flowchart showing migration phases

**Process**: Phase breakdown, rollback triggers, validation checkpoints
</structured-document>

---

## Process Instructions (NOT included in design.md)

### Visual Design Guidelines
**Include based on complexity**: 
- **Simple features**: Basic component diagram or none if trivial
- **Complex features**: Architecture diagram, data flow diagram, ER diagram (if complex)
- **When helpful**: State machines, component interactions, decision trees, process flows, auth flows, approval workflows, data pipelines

**Mermaid Diagram Rules**:
- Use only basic graph syntax with nodes and relationships
- Exclude all styling elements (no style definitions, classDef, fill colors)
- Avoid visual customization (backgrounds, custom CSS)
- Example: `graph TB` → `A[Login] --> B[Dashboard]` → `B --> C[Settings]`

### Quality Checklist
- [ ] Requirements covered with traceability
- [ ] Existing implementation respected
- [ ] Steering compliant, deviations documented
- [ ] Architecture visualized with clear diagrams
- [ ] Components have Purpose, Key Features, Interface Design
- [ ] Data models individually documented
- [ ] Integration with existing system explained

### 3. Design Document Generation & Metadata Update
- Generate complete design document following structure guidelines
- Update `.kiro/specs/$1/spec.json`:
```json
{
  "phase": "design-generated", 
  "approvals": {
    "requirements": { "generated": true, "approved": true },
    "design": { "generated": true, "approved": false }
  },
  "updated_at": "current_timestamp"
}
```

### Actionable Messages
If requirements are not approved and no `-y` flag ($2 != "-y"):
- **Error Message**: "Requirements must be approved before generating design. Run `/kiro:spec-requirements $1` to review requirements, then run `/kiro:spec-design $1 -y` to proceed."
- **Alternative**: "Or run `/kiro:spec-design $1 -y` to auto-approve requirements and generate design."

### Conversation Guidance
After generation:
- Guide user to review design narrative and visualizations
- Suggest specific diagram additions if needed
- Direct to run `/kiro:spec-tasks $1 -y` when approved

Create design document that tells complete story through clear narrative, structured components, and effective visualizations. think deeply