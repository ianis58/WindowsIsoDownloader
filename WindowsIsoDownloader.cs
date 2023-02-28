using Microsoft.Playwright;
using WindowsIsoDownloader.Extension;

var isoDownloadFolder = "c:\\WindowsIsoDownloader\\";
var isoFilename = "windows11.iso";

Directory.CreateDirectory(isoDownloadFolder);

using var playwright = await Playwright.CreateAsync();
var firefox = playwright.Firefox;
var browser = await firefox.LaunchAsync(new BrowserTypeLaunchOptions());

var page = await browser.NewPageAsync();

// Opening the main Windows 11 download page
await page.GotoAsync("https://www.microsoft.com/en-us/software-download/windows11/");

// Selecting the Windows edition
await page.SelectOptionAsync("#product-edition", "2370");

// Clicking on the button to validate the Windows edition
await page.ClickAsync("#submit-product-edition");

// Selecting the Windows language (waiting some time to be sure the page is ready)
await page.WaitForTimeoutAsync(2500);
await page.SelectOptionAsync("#product-languages", """{"id":"14965","language":"English"}""");

// Clicking on the button to validate the Windows language
await page.ClickAsync("#submit-sku");

// Obtaining the iso download link (waiting some time to be sure the page is ready)
await page.WaitForTimeoutAsync(4500);
var elementHandle = await page.QuerySelectorAsync("#card-info-content .button");

if(null == elementHandle)
{
    Console.WriteLine("Error: download button was not found.");
}
else
{
    var isoFileUrl = await elementHandle.EvaluateAsync<string>("element => element.href");

    if (isoFileUrl.Contains(".iso"))
    {
        using(var httpClient = new HttpClient())
        {
            httpClient.Timeout = TimeSpan.FromHours(2);

            using (var filestream = new FileStream(Path.Combine(isoDownloadFolder, isoFilename), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var progress = new Progress<float>();
                progress.ProgressChanged += ProgressChanged;
                await httpClient.DownloadAsync(isoFileUrl, filestream, progress);
            }
        }
    }
    else
    {
        Console.WriteLine($"Error: found a download link but it seems incorrect: {isoFileUrl}");
    }
}

void ProgressChanged(object? sender, float e)
{
    Console.WriteLine($"ISO download progress: {e.ToString("p1")}  ");
}
