---
title: Architecture Design
category: blueprint
---

# Veeling — Architecture Overview

## System Overview

Veeling is a **.NET 10 modular monolith CLI** distributed as a global `dotnet` tool.

The system is organized in clear layers:

1. **Composition Root** (`Program`, `App`, DI setup)
2. **CLI Adapters** (`Commands/` using `System.CommandLine`)
3. **Application Services** (`Core/Application/` orchestration)
4. **Domain/Project Runtime** (`Project`, `VSchema`, jobs and modifiers)
5. **Provider Abstractions** (project data session and LLM providers)
6. **Shared Models** (`Veeling.Models`)

This preserves a single deployable process while enforcing separations of concern.

## Module Responsibilities

| Module | Responsibility |
|:--|:--|
| `Veeling.CLI/Program.cs` | Process entrypoint, host bootstrap, UTF-8 console configuration. |
| `Veeling.CLI/App.cs` | Root command composition and top-level exception handling. |
| `Veeling.CLI/Commands/` | Parse CLI args/options and delegate to application services. |
| `Veeling.CLI/Core/Application/` | Use-case orchestration (`init`, `config`, `modify`, `export`, `status`, `translate`, `publish`). |
| `Veeling.CLI/Providers/` | Filesystem persistence implementation and LLM provider factory/implementations (OpenAI, Gemini, Claude). |
| `Veeling.CLI/*Job*` and helpers | Translation/status workflows, record mutation, publishing, glossary handling. |
| `Veeling.Models/` | Shared model contracts, YAML (de)serialization, status/hash semantics, language/style primitives. |

## Service Boundaries

### Boundary 1 — CLI Adapter Layer
- Owns user interaction, options, and console output formatting.
- Must not implement business rules directly.
- Calls application services with typed request models.

### Boundary 2 — Application Layer
- Owns orchestration and command workflows.
- Coordinates project sessions, jobs, and domain operations.
- Depends on abstractions (`IProjectDataSessionFactory`, `ILLMProviderFactory`) rather than concrete infrastructure.

### Boundary 3 — Infrastructure Providers
- Implements filesystem-backed persistence and external AI integrations.
- Encapsulates provider-specific configuration and request/response handling.
- Exposes stable interfaces to the application layer.

### Boundary 4 — Shared Models
- Defines durable data contracts used across CLI, services, and providers.
- Centralizes YAML schema compatibility and metadata semantics.

### Dependency Rule

Dependencies flow inward:

`Commands -> Core/Application -> Providers Interfaces + Domain Runtime -> Models`

No layer should depend on a higher-level adapter concern.

## Technology Stack

- **Runtime:** .NET 10
- **CLI framework:** `System.CommandLine`
- **Hosting/DI:** `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`
- **Persistence format:** YAML via `YamlDotNet`
- **Primary storage:** local filesystem (project-local + user-global config)
- **LLM integrations:** OpenAI, Gemini, Claude

## Data Flow

### Command Flow (general)

1. User invokes `veeling <command>`.
2. Command adapter parses input and validates basic shape.
3. Application service executes use-case orchestration.
4. Service opens a project data session through `IProjectDataSessionFactory`.
5. Session reads/writes YAML-backed records and schemas.
6. For `translate`, service creates a translation job that calls an LLM provider.
7. Writes are persisted atomically to filesystem-backed YAML files.

### Translation Flow (specific)

1. Resolve source and target languages.
2. Load source records from `data/<schema>.<from>.yaml`.
3. Build prompt context from project metadata, schema fields, and optional glossary.
4. Request completion from configured LLM provider.
5. Create missing target records with metadata status `NeedsReview`.
6. Store `SourceHash` to detect later source drift.

## Architectural Patterns

- **Layered Monolith:** single process, explicit layer boundaries.
- **Ports and Adapters (lightweight):** provider interfaces for persistence and LLM runtime integrations.
- **Composition Root + DI:** all services wired centrally via `AddVeelingCli`.
- **Filesystem-First State:** no DB/ORM; YAML files are canonical data source.
- **Atomic File Writes:** reduces corruption risk during saves.

## Error Handling & Resilience

- Domain/command errors are represented with dedicated exception types and converted to user-facing CLI output.
- Command execution returns explicit exit codes for automation friendliness.
- Project/session operations validate schema and language support before mutation.
- Translation response parsing guards against invalid JSON payloads from model output.
- Persisted writes use atomic replacement to reduce partial-write failure modes.
