using System.CommandLine;

namespace Veeling.CLI.Commands;

internal interface ICliCommand
{
    Command Command { get; }
}
