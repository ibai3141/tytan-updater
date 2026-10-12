using Tytan.Updater.Desktop;

namespace Tytan.Updater.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
        {
            return await CommandChecks.RunAsync();
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;

        try
        {
            using var api = new CloudApiClient();
            return await Command.RunAsync(args, api, Console.Out, Console.Error,
                () => new Credentials(
                    Environment.GetEnvironmentVariable("TYTAN_API_USERNAME") ?? "TytanSQL",
                    Environment.GetEnvironmentVariable("TYTAN_API_PASSWORD") ?? ""), cancellation.Token);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine("Error: " + error.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }
}
