using PuppeteerSharp;
using WindowsIsoDownloader.Extension;

using var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
var browser = await Puppeteer.LaunchAsync(new LaunchOptions());

var pages = await browser.PagesAsync();
var page = pages.First();

// Opening the main Windows 11 download page
await page.GoToAsync("https://www.microsoft.com/en-us/software-download/windows11/");

// Selecting the Windows edition
await page.SelectAsync("#product-edition", "2370");

// Clicking on the button to validate the Windows edition
await page.ClickAsync("#submit-product-edition");

// Selecting the Windows language (waiting some time to be sure the page is ready)
await page.WaitForTimeoutAsync(2500);
await page.SelectAsync("#product-languages", """{"id":"14965","language":"English"}""");

// Clicking on the button to validate the Windows language
await page.ClickAsync("#submit-sku");

// Obtaining the iso download link (waiting some time to be sure the page is ready)
await page.WaitForTimeoutAsync(4500);
var hrefFromLinks = await page.QuerySelectorAllHandleAsync("#card-info-content .button").EvaluateFunctionAsync<string[]>("elements => elements.map(a => a.href)");

if(hrefFromLinks.Any() && hrefFromLinks.First().Contains(".iso"))
{
    var isoFileUri = hrefFromLinks.First();
    
    using(var httpClient = new HttpClient())
    {
        httpClient.Timeout = TimeSpan.FromHours(2);

        using (var filestream = new FileStream("windows11.iso", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var progress = new Progress<float>();
            progress.ProgressChanged += ProgressChanged;
            await httpClient.DownloadAsync(isoFileUri, filestream, progress);
        }
    }
}
else
{
    Console.WriteLine("Error: download button was not found.");
}

void ProgressChanged(object? sender, float e)
{
    Console.WriteLine($"ISO download progress: {e.ToString("p1")}  ");
}
