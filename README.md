# Veeling

An AI-powered translation management tool with a command-line interface.

## Installation

Install Veeling as a global .NET tool from NuGet:

```bash
dotnet tool install --global veeling
```

If you already have Veeling installed, update it with:

```bash
dotnet tool update --global veeling
```

Manual advisory check:

```bash
veeling update check
```

User-controlled self-update guidance (never auto-mutates install):

```bash
veeling update self
```

If your machine has multiple NuGet feeds configured, you can enforce NuGet.org explicitly:

```bash
dotnet tool install --global veeling --add-source https://api.nuget.org/v3/index.json
```

Contributor-only local packaging smoke test scripts are still available in this repository (`install.sh`, `update.sh`, `Install.ps1`, `Update.ps1`).

For standalone archive installation and maintainer archive release generation, see `docs/installation.md`.

For public release expectations, upgrade/rollback guidance, and support/security routing, see `docs/release-upgrade.md`.

## Usage

Running `veeling init` will ask you a few questions and scaffold a new project.

Type `veeling -h` for a full list of commands.

### Natural-language intent command (v1)

Use `ai` (or alias `ask`) to resolve plain-language intent into one validated Veeling command:

```bash
veeling ai "translate everything to portuguese"
veeling ask "show project status"
```

v1 safety behavior:

- Always shows a canonical command preview first.
- Requires explicit confirmation before execution.
- Resolves to one existing Veeling command only (no chaining/shell execution).
- Rejects recursive self-targeting (`ai`/`ask`).
- Treats interactive/meta commands (`onboard`, `update`) as suggestion-only.

## AI setup (start here)

Run onboarding first:

```bash
veeling onboard
```

The onboarding flow is the primary AI setup entrypoint in v1. It guides you through:

- Provider selection (`openai`, `gemini`, `claude`)
- Model selection (curated defaults + `Other` free-text)
- API key capture (plain terminal input in v1)
- Claude `max tokens` when Claude is selected
- Global config persistence and an immediate connectivity verification call

### v1 onboarding decisions

- Command surface is `veeling onboard` only
- API key entry is plain input (masking is future hardening)
- Writes go to global config (`~/.veeling.yaml`) in v1
- Verification runs through the same provider abstraction used at runtime

## LLM Configuration

Veeling reads LLM settings from `.veeling.yaml` files (not environment variables).

Config precedence:

1. Local project config: `<project>/.veeling.yaml`
2. Global user config: `~/.veeling.yaml`

If `llm_provider` is not set, Veeling defaults to `openai`.

Intent parser configuration (optional):

- `intent_parser_provider` — optional parser provider override.
  - If unset, falls back to `llm_provider`.
- `intent_parser_model` — optional parser model override.
  - If unset, falls back to the selected provider's normal model key (`openai_model`, `gemini_model`, `claude_model`).

Supported keys by provider:

- `llm_provider=openai`
  - `openai_apikey`
  - `openai_model` (for example `gpt-4.1-mini`)
- `llm_provider=gemini`
  - `gemini_apikey`
  - `gemini_model` (for example `gemini-2.5-flash`)
- `llm_provider=claude`
  - `claude_apikey`
  - `claude_model` (for example `claude-sonnet-4-5`)
  - `claude_max_tokens` (optional, defaults to `4096`)

Advanced/manual configuration is still available via `veeling config`.

## Example Workflow

First, scaffold a project:

```text
$ veeling init
Project name: my_project
Description (end with a single '.' on a line):
Project that is used to demonstrate how Veeling works with automated machine translations.
.
Languages (comma-separated 2-letter language codes, i.e. 'en,fr,de'): en,fi
Master language (2-letter language code, i.e. 'en'): en
Tone:
1. neutral
2. casual
3. professional
4. playful
Your choice: 1
Formality:
1. informal
2. neutral
3. formal
Your choice: 2
Audience (press Enter for 'general'):

Summary:
- Name: my_project
- Description: Project that is used to demonstrate how…
- Languages: English, Finnish
- Master Language: English
- Style:
  - Tone: neutral
  - Formality: neutral
  - Audience: general

About to create directory C:\Projects\Veeling\projects\my_project
Proceed? [y/N] y
Scaffolding project at C:\Projects\Veeling\projects\my_project
```

This will create the project file and an example schema called **Schema1**, that has one field, **Field1**.

Now, let's write some data:

```text
$ veeling modify Schema1.Field1:en --value="Sample value"
```

and verify that it's there:

```text
$ veeling export Schema1.Field1:en
Schema1.Field1:en: Sample value
```

Now, we can check the status of our project:

```text
$ veeling status
Missing:        Schema1.Field1:fi
```

It shows that we are missing a translation in Finnish for this field. Let's fix it using AI:

```text
$ veeling translate --to=fi
Processing schema 'Schema1' ('en' -> 'fi')...
Translated field Field1: Esimerkkiarvo
Saving changes... ok
```

Let's fetch the values:

```text
$ veeling export Schema1.*:*
Schema1.Field1:en: Sample value
Schema1.Field1:fi: Esimerkkiarvo
```

Finally, let's publish the project:

```text
$ veeling publish > strings.json
```

This is a file you can integrate in your app deployment workflow.

## Example Schema

Schemas live in the main project directory and are named &lt;schemaName&gt;.schema.yaml

```yaml
name: SignIn

description: >
  The "Sign In" page on the website, displayed when a user tries to access a protected page.

model:
  - name: username
    description: The label for the username field.

  - name: password
    description: The label for the password field.
```

## Example Data

Data files live in the "data" subdirectory under the main project directory and are named &lt;schemaName&gt;.&lt;lang&gt;.yaml where &lt;lang&gt; is two-letter language code like 'en', 'de' or 'fi'.

```yaml
- name: username
  value: Username (email)
- name: password
  value: Password
```

## Glossary Files

Glossary files are optional, live in the main project directory, and are named `glossary.<lang>.yaml`, where `<lang>` is the target language code.

`language` inside the file must match the `<lang>` part in the filename.

`term` values are always defined in the project master language.

Example:

```yaml
language: fi

entries:
  - term: Sign in
    translation: Kirjaudu sisaan
    status: approved
    note: Use as the primary button label.
    forbidden_variants:
      - Kirjautuminen
    applies_to:
      - ui
      - system
      - ai
```

Entry fields:

- `term` (required): source term in master language
- `translation` (required): preferred target-language translation
- `status` (required): one of `approved`, `preferred`, `deprecated`, `forbidden`
- `note` (optional): extra context for human/AI translators
- `forbidden_variants` (optional): target-language variants to avoid
- `applies_to` (optional): any of `ui`, `system`, `ai`; if omitted, defaults to all

Status guidance:

- `approved`: use this translation whenever applicable
- `preferred`: prefer this translation unless context clearly requires another
- `deprecated`: avoid this translation; use a better alternative
- `forbidden`: never use this translation or any listed forbidden variant

### Using Glossary with `translate`

Glossary rules are loaded automatically for the target language during `veeling translate`.

Best quality is achieved when translating from the project master language.

If you translate from a non-master source language, Veeling will continue but print a warning: glossary terms are then treated as soft hints and translation quality may be lower.
