using Microsoft.Playwright;
using System.Text.Json;
using WindowsIsoDownloader;
using WindowsIsoDownloader.Extension;

var jsonConfig = File.ReadAllText("config.json");
Config? config = null;

try
{
    config = JsonSerializer.Deserialize<Config>(jsonConfig);
}
catch(Exception e)
{
    ErrorLoadingConfig();
    return -1;
}

if (null == config
    || string.IsNullOrWhiteSpace(config.DownloadFolder)
    || string.IsNullOrWhiteSpace(config.DownloadFilename)
    || !config.Actions.Any())
{
    ErrorLoadingConfig();
    return -1;
}

Directory.CreateDirectory(config.DownloadFolder);

Console.WriteLine("[Info] Trying to obtain Windows 11 iso download link...");

using var playwright = await Playwright.CreateAsync();
var firefox = playwright.Firefox;
var browser = await firefox.LaunchAsync(new BrowserTypeLaunchOptions());

var page = await browser.NewPageAsync();

var actions = config.Actions.OrderBy(x => x.Order);

IElementHandle? downloadButton = null;

foreach(var action in actions)
{
    if(action.WaitBeforeAction.HasValue && action.WaitBeforeAction.Value > 0)
    {
        await page.WaitForTimeoutAsync(action.WaitBeforeAction.Value);
    }

    if(action.Kind == "Goto")
    {
        if (string.IsNullOrEmpty(action.Parameters.Url))
        {
            throw new ArgumentNullException("The Url parameter for the Goto action is not specified.", innerException: null);
        }
        await page.GotoAsync(action.Parameters.Url);
    }
    else if (action.Kind == "SelectOption")
    {
        if (string.IsNullOrEmpty(action.Parameters.Selector) || string.IsNullOrEmpty(action.Parameters.Values))
        {
            throw new ArgumentNullException("The Selector and/or the Values parameter(s) for the SelectOption action is/are not specified.", innerException: null);
        }
        await page.SelectOptionAsync(action.Parameters.Selector, action.Parameters.Values);
    }
    else if (action.Kind == "Click")
    {
        if (string.IsNullOrEmpty(action.Parameters.Selector))
        {
            throw new ArgumentNullException("The Selector parameter for the Click action is not specified.", innerException: null);
        }
        await page.ClickAsync(action.Parameters.Selector);
    }
    else if (action.Kind == "QuerySelector")
    {
        if (string.IsNullOrEmpty(action.Parameters.Selector))
        {
            throw new ArgumentNullException("The Selector parameter for the QuerySelector action is not specified.", innerException: null);
        }
        downloadButton = await page.QuerySelectorAsync(action.Parameters.Selector);
    }
}

float currentProgress = 0;

if(null == downloadButton)
{
    Console.WriteLine("[Error] Download button was not found.");
}
else
{
    var isoFileUrl = await downloadButton.EvaluateAsync<string>("element => element.href");

    if (isoFileUrl.Contains(".iso"))
    {
        Console.WriteLine($"[Success] Download link found: {isoFileUrl}");
        Console.WriteLine("[Info] Windows 11 iso download in progress...");
        using(var httpClient = new HttpClient())
        {
            httpClient.Timeout = TimeSpan.FromHours(2);

            using (var filestream = new FileStream(Path.Combine(config.DownloadFolder, config.DownloadFilename), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var progress = new Progress<float>();
                progress.ProgressChanged += ProgressChanged;
                await httpClient.DownloadAsync(isoFileUrl, filestream, progress);
            }
        }
    }
    else
    {
        Console.WriteLine($"[Error] Found a download link but it seems incorrect: {isoFileUrl}");
    }
}

Console.WriteLine();
Console.WriteLine("[Success] ISO Downloaded successfully! Exiting with code 0.");

return 0;

void ProgressChanged(object? sender, float e)
{
    // The progress in the e variable is in the range 0.000 (0%) to 1.000 (100%)
    float reportedProgress = (float)Math.Round(e, 3);

    if(reportedProgress > currentProgress)
    {
        currentProgress = reportedProgress;
        for(int i = 0; i < 100; i++)
        {
            Console.Write((i < (int)(currentProgress * 100)) ? "█" : "░");
        }
        Console.Write($" {currentProgress.ToString("p1")}");
        Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
    }
}

void ErrorLoadingConfig()
{
    Console.WriteLine("[Fatal] Unable to load the configuration. Exiting with code -1.");
}
