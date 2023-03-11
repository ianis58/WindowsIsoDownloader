namespace WindowsIsoDownloader
{
    internal class Config
    {
        public string DownloadFolder { get; set; }
        public string DownloadFilename { get; set; }
        public List<BrowserAction> Actions { get; set; }
    }

    internal class BrowserAction
    {
        public int Order { get; set; }
        public string Kind { get; set; }
        public int? WaitBeforeAction { get; set; }
        public BrowserActionParameters Parameters { get; set; }
    }

    internal class BrowserActionParameters
    {
        public string? Url { get; set; }
        public string? Selector { get; set; }
        public string? Values { get; set; }
    }
}
