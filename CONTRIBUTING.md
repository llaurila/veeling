# Contributing to Veeling

Thanks for contributing to Veeling. This guide summarizes the patterns already used in the repository so new changes fit the existing codebase.

## Prerequisites

- Install the .NET 10 SDK. All projects target `net10.0`, and the repository does not currently pin an SDK with `global.json`.
- Use GitHub pull requests for review.
- If you work on translation or provider features, set up the provider you need in `.veeling.yaml` as described in `README.md`.

## License and contribution policy

- Veeling is licensed under Apache-2.0.
- Contributor policy is **inbound = outbound**:
  - By submitting a contribution, you agree your contribution is licensed under the same Apache-2.0 license used by the project.
  - No separate CLA is required at this time.

## Repository layout

- `Veeling.CLI/` - command-line application, command adapters, application services, providers, prompts, and file operations.
- `Veeling.Models/` - shared domain and YAML model types.
- `Veeling.CLI.Tests/` - CLI, service, and integration-style tests.
- `Veeling.Models.Tests/` - unit tests for shared models.
- `README.md` - user-facing installation, configuration, and workflow documentation.
- `docs/architecture.md` - command pipeline and extension guidance.

## Common commands

Run lint/build validation from the repository root:

```bash
dotnet build Veeling.slnx
```

Run targeted tests while iterating:

```bash
dotnet test Veeling.CLI.Tests/Veeling.CLI.Tests.csproj --filter "FullyQualifiedName~<AreaUnderChange>"
```

Run the full test suite before opening a pull request:

```bash
dotnet test Veeling.slnx
```

Build a release package for the global tool:

```bash
dotnet pack -c Release
```

Use the existing install/update scripts when you want to validate tool packaging end to end:

```bash
./install.sh
./update.sh
```

On Windows:

```powershell
.\Install.ps1
.\Update.ps1
```

## Architecture expectations

### CLI commands

- Keep commands in `Veeling.CLI/Commands/`.
- Each command implements `ICliCommand`, builds its `Command` in the constructor, and binds a handler with `Command.SetAction(...)`.
- Keep command handlers focused on input parsing, validation, output, and exit codes.
- Reuse helpers in `Veeling.CLI/Commands/CommandUtils.cs` instead of duplicating option and argument parsing.

### Services and DI

- Put orchestration in application services under `Veeling.CLI/Core/Application/`.
- Register new services and commands in `Veeling.CLI/ServiceCollectionExtensions.cs`.
- Keep business logic out of command handlers whenever possible.

### Models and persistence

- Put shared data and YAML-facing types in `Veeling.Models/`.
- Preserve the existing YAML naming convention based on underscored keys.
- Use `Veeling.CLI/AtomicFile.cs` for persisted YAML or config writes so file updates stay atomic.

## Coding conventions

- Follow the surrounding file style; there is no checked-in `.editorconfig` at the moment.
- Use file-scoped namespaces.
- Keep nullable reference types enabled and preserve explicit null checks and guard clauses.
- Match the repository's modern C# style, including `required`, `init`, collection expressions, target-typed `new`, and primary constructors where they fit naturally.
- Use [] for collection initializers when it's more concise and readable.
- Prefer small, focused files grouped by responsibility.
- Avoid unrelated refactors in the same change.

## Command behavior conventions

- Write normal output to stdout.
- Write actionable user errors to stderr.
- Return explicit exit codes: `0` for success, non-zero for failure.
- Let shared top-level exception handling in `Veeling.CLI/App.cs` handle domain-level failures when possible.

## Tests

- Run `dotnet test Veeling.slnx` before opening a pull request.
- Add tests in the project closest to the change:
  - `Veeling.CLI.Tests/` for commands, services, providers, and end-to-end CLI behavior.
  - `Veeling.Models.Tests/` for model and domain behavior.
- Follow the existing test naming style, such as `Action_Scenario_ExpectedResult`.
- Reuse helpers in `Veeling.CLI.Tests/TestHelpers.cs` for temp directories, console capture, and service setup.
- Be aware that CLI tests disable parallelization because they manipulate console and current-directory state.
- Match the surrounding test project when adding tests: `Veeling.CLI.Tests/` currently uses xUnit v3, while `Veeling.Models.Tests/` uses xUnit v2.

## Configuration and secrets

- Veeling reads LLM settings from `.veeling.yaml`, not environment variables.
- Local project config overrides global user config.
- Do not commit real API keys or personal settings in `.veeling.yaml`.
- If you add or change config behavior, update `README.md` so user-facing setup instructions stay current.

## Documentation

- Update `README.md` for user-visible command, config, or workflow changes.
- Update `docs/architecture.md` when you change command structure, DI wiring, or application-layer patterns.
- Keep examples and command output in docs aligned with the current CLI behavior.

## Branching and pull requests

- Create branches from `development` using `feat/<id>-<slug>` or `fix/<id>-<slug>`.
- Keep commits focused and easy to review.
- Open a GitHub pull request against `development`.
- Fill out the pull-request template and include verification notes.
- Link the related issue or tracking item in your pull request description.

## Commits and pull requests

- Match the existing commit style: short, descriptive subjects such as `service layer`, `atomic file writes`, or `claude support`.
- Include tests with code changes and mention any manual verification in the pull request description when relevant.
- CI is a quality gate, but it does not replace local verification.

## Adding a new command

When adding a command:

1. Create the command in `Veeling.CLI/Commands/`.
2. Implement `ICliCommand`.
3. Define options and arguments in the constructor.
4. Reuse `CommandUtils` where possible.
5. Delegate orchestration to an application service.
6. Register the command in `Veeling.CLI/ServiceCollectionExtensions.cs`.
7. Add tests for success paths, validation failures, and exit codes.

For more detail on command authoring, start with `docs/architecture.md`.

For architecture and process context, also review:

- `docs/architecture.md`
- `docs/standards.md`
