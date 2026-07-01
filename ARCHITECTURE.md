# Veeling Architecture

Veeling is a .NET 10 command-line application built around dependency injection and `System.CommandLine`.
The CLI entrypoint wires services into a host, discovers command implementations through DI, and then
invokes the root command.

## Command Pipeline

Command execution flows through three core pieces:

1. `Veeling.CLI/Program.cs`
   - Creates the generic host (`Host.CreateDefaultBuilder(args)`).
   - Registers CLI services via `services.AddVeelingCli(ctx.Configuration)`.
   - Resolves `App` from DI and calls `RunAsync(args)`.

2. `Veeling.CLI/ServiceCollectionExtensions.cs`
   - Registers shared services/factories used by commands.
   - Registers every command as `ICliCommand`.
   - Registers `App`.

3. `Veeling.CLI/App.cs`
   - Creates a `RootCommand`.
   - Enumerates all `ICliCommand` instances from DI and adds their `Command` objects as subcommands.
   - Parses args and invokes the command tree.
   - Converts known domain exceptions into user-friendly stderr output and exit codes.

## Command Contract

All commands in `Veeling.CLI/Commands` implement `ICliCommand`:

```csharp
internal interface ICliCommand
{
    Command Command { get; }
}
```

This keeps command discovery simple: if a class implements `ICliCommand` and is registered in DI,
it becomes part of the CLI surface.

## How Commands Are Implemented

Commands in `Veeling.CLI/Commands/*.cs` follow a consistent structure:

1. Define options/arguments as private readonly fields.
2. Build the `Command` in the constructor:
   - set name and description,
   - add options/arguments,
   - bind handler with `Command.SetAction(Execute)`.
3. Keep `Execute(ParseResult parseResult)` focused on orchestration:
   - read and validate inputs,
   - call services/jobs,
   - write user output,
   - return explicit exit codes.

Representative examples:

- `StatusCommand` and `TranslateCommand` inject factories/services and delegate heavy work to jobs.
- `ExportCommand` and `ModifyCommand` parse input and apply focused domain operations.
- `ConfigCommand` and `InitCommand` contain command-specific interaction and validation.

## Application Layer (`Veeling.Core.Application`)

Veeling now has an explicit application-service layer that sits between command adapters and lower-level
services/jobs.

- Commands in `Veeling.CLI/Commands` are adapters:
  - parse CLI input,
  - call an application service,
  - map service results/errors to terminal output and exit codes.
- Application services in `Veeling.CLI/Core/Application` coordinate use cases and isolate orchestration from
  `System.CommandLine` and console-specific concerns.

Current application services:

- `InitApplicationService`
- `ConfigApplicationService`
- `PublishApplicationService`
- `StatusApplicationService`
- `ExportApplicationService`
- `ModifyApplicationService`
- `TranslateApplicationService`

## Atomic File Writes

All write paths that persist project/config/data YAML now use `Veeling.CLI/AtomicFile.cs`.

- Write strategy:
  1. write content to a temporary file in the same directory,
  2. replace existing destination atomically (`File.Replace`) when it exists,
  3. otherwise move the temp file into place.
- This avoids partially written files on interruption and keeps writes resilient.

Current atomic-write call sites include:

- `ProjectInitializer` scaffold outputs (`Project.yaml`, schema, glossary files)
- `Project.SaveData`
- `FileSystemProjectDataSession.SaveChanges`
- `VeelingConfig` global/local config persistence

## Shared Command Utilities

`Veeling.CLI/Commands/CommandUtils.cs` centralizes common CLI concerns:

- project file option creation (`--project-file`, `-p`),
- record spec argument creation,
- project lookup and default path handling,
- target language parsing,
- safe record spec parsing.

Using these helpers avoids duplicated option definitions and keeps behavior consistent across commands.

## Best Practices For Authoring New Commands

When adding a new command, follow this checklist:

1. Create a class under `Veeling.CLI/Commands` implementing `ICliCommand`.
2. Inject collaborators (services/factories/providers) through constructor injection.
3. Define command metadata and bind handler in the constructor.
4. Reuse `CommandUtils` for shared options/arguments whenever possible.
5. Validate user input early; fail fast with clear stderr messages.
6. Return deterministic exit codes (`0` success, non-zero on failure).
7. Keep business logic in services/jobs; command handlers should coordinate, not own domain logic.
8. Register the command in `AddVeelingCli`:
   - `services.AddSingleton<ICliCommand, YourNewCommand>();`

### Error Handling Guidance

- Prefer explicit input validation and user-facing error messages in the command.
- Catch expected argument/file errors near the command boundary and return non-zero.
- Let shared top-level handling in `App.RunAsync` process domain-level exceptions (`VeelingException`,
  `CommandExecutionException`) consistently.

### Output Guidance

- Use `Console.WriteLine` for normal output.
- Use `Console.Error.WriteLine` for actionable user errors.
- Keep messages specific and operational (what failed and what user should fix).

## Minimal Skeleton

```csharp
using System.CommandLine;

namespace Veeling.CLI.Commands;

public class ExampleCommand : ICliCommand
{
    private readonly SomeService service;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    public ExampleCommand(SomeService service)
    {
        this.service = service;

        Command = new Command("example", "Run the example operation.");
        Command.Options.Add(projectFileOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null) return 1;

        service.Run(project);
        return 0;
    }
}
```

After creating this class, register it in `Veeling.CLI/ServiceCollectionExtensions.cs` as an
`ICliCommand` implementation.
