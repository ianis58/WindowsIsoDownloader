using WindowsIsoDownloader;
using WindowsIsoDownloader.Cli;

// Allow the Unicode progress bar (█ / ░) to render correctly on every OS/terminal.
try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}
catch
{
    // Some environments (e.g. redirected output) do not support changing the encoding.
}

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (ArgumentException e)
{
    Console.WriteLine($"[Fatal] {e.Message}");
    return ExitCodes.GeneralError;
}

if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.HelpText);
    return ExitCodes.Success;
}

using var cancellation = new CancellationTokenSource();

// Allow Ctrl+C to stop gracefully during both the browser and the download phases.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    var app = new App(options);
    return await app.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("[Warn] Operation cancelled.");
    return ExitCodes.Cancelled;
}
