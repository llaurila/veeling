using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests;

public class AppTests
{
    [Fact]
    public void RootCommand_ContainsAllRegisteredCommands()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string[] commandNames = [.. app.RootCommand.Subcommands.Select(command => command.Name)];

        Assert.Equal(
            ["init", "config", "publish", "status", "export", "modify", "translate", "onboard", "update", "ai"],
            commandNames);
    }

    [Fact]
    public async Task RunAsync_TriggersBackgroundUpdateCheck_NonBlocking()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        await app.RunAsync(["-h"]);

        UpdateCheckBootstrapService bootstrap = serviceProvider.GetRequiredService<UpdateCheckBootstrapService>();
        Assert.NotNull(bootstrap);
    }

    [Fact]
    public async Task RunAsync_Help_DoesNotEmitHttpClientInfoLogs_AndStillChecksForUpdates()
    {
        var logSink = new InMemoryLogSink();
        var settings = new Dictionary<string, string?>
        {
            ["UpdateCheck:TimeoutSeconds"] = "2",
            ["UpdateCheck:CacheTtlHours"] = "1"
        };

        ServiceCollection services = new();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new InMemoryLoggerProvider(logSink));
        });
        services.AddVeelingCli(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<IUpdateCheckCache, InMemoryUpdateCheckCache>();

        var handler = new SignalingHandler();
        services.AddHttpClient(ReleaseMetadataClientRegistration.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new();
        int code = await app.RunAsync(["--help", "--check-updates"]);
        Assert.Equal(0, code);

        await handler.WaitForCallAsync(TimeSpan.FromSeconds(3));

        Assert.DoesNotContain(
            logSink.Entries,
            static entry => entry.LogLevel == LogLevel.Information
                && entry.Category.StartsWith(ReleaseMetadataClientRegistration.HttpClientLoggerCategoryPrefix, StringComparison.Ordinal));
    }

    private sealed class SignalingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> called = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string payload = """
            {
              "schema_version": "1.0",
              "generated_at": "2026-06-26T00:00:00Z",
              "source": {
                "repository": "https://github.com/llaurila/veeling",
                "releases_url": "https://github.com/llaurila/veeling/releases"
              },
              "channels": {
                "stable": {
                  "version": "0.4.1",
                  "tag": "v0.4.1",
                  "published_at": "2026-06-26T00:00:00Z",
                  "release_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
                  "notes_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
                  "changelog_url": "https://github.com/llaurila/veeling/blob/main/CHANGELOG.md",
                  "compatibility": {
                    "minimum_cli_version": null,
                    "notes": "No compatibility floor declared yet."
                  }
                },
                "prerelease": null
              }
            }
            """;

            called.TrySetResult(true);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        }

        public async Task WaitForCallAsync(TimeSpan timeout)
        {
            await called.Task.WaitAsync(timeout);
        }
    }

    private sealed record LogEntry(string Category, LogLevel LogLevel, string Message);

    private sealed class InMemoryLogSink
    {
        private readonly ConcurrentBag<LogEntry> entries = [];

        public IReadOnlyCollection<LogEntry> Entries => entries;

        public void Add(string category, LogLevel logLevel, string message)
        {
            entries.Add(new LogEntry(category, logLevel, message));
        }
    }

    private sealed class InMemoryLoggerProvider(InMemoryLogSink sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, sink);

        public void Dispose()
        {
        }
    }

    private sealed class InMemoryLogger(string categoryName, InMemoryLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Add(categoryName, logLevel, formatter(state, exception));
        }
    }

    private sealed class InMemoryUpdateCheckCache : IUpdateCheckCache
    {
        private UpdateCheckCacheEntry? entry;

        public UpdateCheckCacheEntry? Read() => entry;

        public void Write(UpdateCheckCacheEntry entry)
        {
            this.entry = entry;
        }
    }
}
