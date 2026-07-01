using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Veeling.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, services) =>
            {
                services.AddVeelingCli(ctx.Configuration);
            })
            .Build();

        App app = host.Services.GetRequiredService<App>();
        return await app.RunAsync(args);
    }
}
