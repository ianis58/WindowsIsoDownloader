using System.Text.Json.Serialization;

namespace WindowsIsoDownloader.Configuration
{
    /// <summary>
    /// Strongly typed representation of <c>config.json</c>. It drives the whole
    /// download workflow: where to save the ISO, how to retry and which browser
    /// actions to run.
    /// </summary>
    public sealed class Config
    {
        /// <summary>Folder where the ISO file will be written. Works on Windows and Linux.</summary>
        public string DownloadFolder { get; set; } = ".";

        /// <summary>File name (not the full path) of the produced ISO.</summary>
        public string DownloadFilename { get; set; } = "windows10.iso";

        /// <summary>
        /// Image architecture to prefer when the page offers more than one ISO
        /// (the Windows 10 page exposes both a 32-bit and a 64-bit image).
        /// </summary>
        public PreferredArchitecture PreferredArchitecture { get; set; } = PreferredArchitecture.X64;

        /// <summary>How many times the whole browser workflow is attempted before giving up.</summary>
        public int Attempts { get; set; } = 3;

        /// <summary>Base delay (seconds) between attempts. It grows exponentially (capped at 60 s).</summary>
        public int RetryDelaySeconds { get; set; } = 5;

        /// <summary>Run the browser without a visible window (default). Use <c>--headed</c> to override.</summary>
        public bool Headless { get; set; } = true;

        /// <summary>
        /// Opt-in, <b>experimental</b> warm-up of Microsoft's anti-bot ("Sentinel")
        /// handshake. It is best-effort and may not be enough on its own.
        /// </summary>
        public bool ExperimentalSentinel { get; set; } = false;

        /// <summary>Ordered list of browser actions used to reach the download link.</summary>
        public List<BrowserAction> Actions { get; set; } = new();
    }

    /// <summary>Preferred image architecture when several ISO links are available.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PreferredArchitecture>))]
    public enum PreferredArchitecture
    {
        /// <summary>64-bit image (default).</summary>
        X64,

        /// <summary>32-bit (x86) image.</summary>
        X86,

        /// <summary>ARM64 image.</summary>
        Arm64,

        /// <summary>Do not filter: use the first ISO link found.</summary>
        Any,
    }

    /// <summary>Supported browser actions.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ActionKind>))]
    public enum ActionKind
    {
        /// <summary>Navigate to a URL.</summary>
        Goto,

        /// <summary>Select an option inside a <c>&lt;select&gt;</c> element.</summary>
        SelectOption,

        /// <summary>Click an element.</summary>
        Click,

        /// <summary>Locate an element (its <c>href</c> is used as the download link).</summary>
        QuerySelector,

        /// <summary>Wait until an element is attached to the DOM.</summary>
        WaitForSelector,
    }

    /// <summary>A single browser interaction (navigation, select, click, query or wait).</summary>
    public sealed class BrowserAction
    {
        /// <summary>Execution order, ascending.</summary>
        public int Order { get; set; }

        /// <summary>Action type.</summary>
        public ActionKind Kind { get; set; }

        /// <summary>Optional delay (milliseconds) applied before running the action.</summary>
        public int? WaitBeforeAction { get; set; }

        /// <summary>
        /// When <c>true</c>, a failure (for example, the element is not present)
        /// is logged and the workflow continues instead of aborting.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>Optional per-action timeout in milliseconds.</summary>
        public int? Timeout { get; set; }

        /// <summary>Action specific parameters.</summary>
        public BrowserActionParameters Parameters { get; set; } = new();
    }

    /// <summary>Parameters accepted by <see cref="BrowserAction"/>.</summary>
    public sealed class BrowserActionParameters
    {
        /// <summary>Target URL, used by the <c>Goto</c> action.</summary>
        public string? Url { get; set; }

        /// <summary>CSS selector, used by <c>SelectOption</c>, <c>Click</c>, <c>QuerySelector</c> and <c>WaitForSelector</c>.</summary>
        public string? Selector { get; set; }

        /// <summary>Value(s) passed to the <c>SelectOption</c> action.</summary>
        public string? Values { get; set; }
    }
}
