---
title: Data Model & Persistence
category: blueprint
---

# Veeling — Data Model

## Entity Overview

Veeling uses a **filesystem-backed YAML data model**. The canonical entities are:

| Entity | Description | Primary Storage |
|:--|:--|:--|
| `ProjectModel` | Project identity, languages, and style. | `Project.yaml` |
| `SchemaModel` | A translation schema (logical section) with field definitions. | `<schema>.schema.yaml` |
| `DataModel` | A translatable field value with optional metadata. | `data/<schema>.<lang>.yaml` |
| `DataMetaModel` | Review/version/hash metadata for a `DataModel`. | nested under `DataModel.meta` |
| `GlossaryModel` | Optional term guidance per target language. | `glossary.<lang>.yaml` |
| `VeelingConfig` | Local/global runtime configuration (user, provider keys, models). | `.veeling.yaml` (project + user home) |

## Schema Definitions

### `Project.yaml` (`ProjectModel`)

Core fields:
- `name: string`
- `description: string`
- `masterLanguage: Language`
- `languages: Language[]`
- `style: Style`
  - `tone: Neutral|Casual|Professional|Playful`
  - `formality: Informal|Neutral|Formal`
  - `audience: string`

### `<schema>.schema.yaml` (`SchemaModel`)

Core fields:
- `name: string`
- `description: string`
- `model: SchemaFieldModel[]`
  - `name: string`
  - `description?: string`
  - `multiline?: bool`

### `data/<schema>.<lang>.yaml` (`DataModel[]`)

Per-entry fields:
- `name: string`
- `value: string`
- `meta?: DataMetaModel`
  - `version?: int`
  - `comment?: string`
  - `status: Unknown|New|NeedsReview|Approved|Bad`
  - `lastUpdate?: datetime`
  - `updatedBy?: string`
  - `hash?: string`
  - `sourceHash?: string`

### `glossary.<lang>.yaml` (`GlossaryModel`)

Core fields:
- `language: Language`
- `entries: GlossaryEntryModel[]`
  - `term: string`
  - `translation: string`
  - `status: Approved|Preferred|Deprecated|Forbidden`
  - `note?: string`
  - `forbiddenVariants: string[]`
  - `appliesTo: Ui|System|Ai` (defaults to all contexts)

### `.veeling.yaml` (config map)

Allowed keys include:
- `username`
- `llm_provider`
- `openai_model`, `openai_apikey`
- `gemini_model`, `gemini_apikey`
- `claude_model`, `claude_apikey`, `claude_max_tokens`

Resolution order: **local project config overrides global user config**.

## Relationships

- One `ProjectModel` has many languages and defines one master language.
- One project contains many `SchemaModel` files.
- Each schema-language pair maps to one data file containing many `DataModel` records.
- A `DataModel` is keyed by `(schema, field, language)` via `RecordLocator` semantics.
- `DataMetaModel.sourceHash` links translated records to a source-language content hash for stale detection.
- One optional glossary file can exist per non-master language.

## Persistence Strategy

- **Storage:** Plain YAML files under project root plus optional user-home config.
- **Session abstraction:** `IProjectDataSession` isolates read/write semantics.
- **Current provider:** `FileSystemProjectDataSession` (configured via provider factory).
- **Write safety:** `AtomicFile.WriteAllText` for save operations.
- **Validation points:**
  - schema existence and shape validation
  - language support checks
  - record filter parsing and locator integrity

No relational database, migrations, or ORM are used.

## State & Lifecycle

### Translation/Review State (`DataStatus`)

- `Unknown` — unset/legacy state.
- `New` — newly created human entry requiring review.
- `NeedsReview` — pending approval (common AI output status).
- `Approved` — reviewed and accepted.
- `Bad` — flagged as incorrect.

### Stale Translation Detection

- `sourceHash` stores a normalized hash of source-language field content.
- If source changes, status workflows can identify target entries needing attention (`SourceChange`).
- Hashing uses deterministic line-ending normalization and field/language-scoped hashing.

### Status Computation (`status` command)

The status job reports:
- `Missing`
- `MissingMaster`
- `SourceChange`
- `NeedsApproval`

This enables quality gates in both manual and automated workflows.
