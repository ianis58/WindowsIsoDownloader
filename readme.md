# Windows 10 ISO Downloader

Windows 10 ISO Downloader is a small cross-platform tool that programmatically
downloads the latest official **Windows 10 ISO** directly from Microsoft's
servers. It drives a real (headless) Firefox browser with
[Playwright](https://playwright.dev/dotnet/) to walk through the download page,
collects the temporary download link and then streams the ISO to disk with a
live progress bar (size, speed and ETA). Downloads are **resumable** — an
interrupted run continues from a `.part` file — and the tool exposes a small
**command line interface** for scripting.

It is useful in projects that build custom Windows 10 installers (for example
[`tiny11builder`](https://github.com/ntdevlabs/tiny11builder)), where the manual
browser steps are the only part that cannot be automated.

The workflow is data-driven (see [`config.json`](#configuration-configjson)), so
the very same tool can also fetch Windows 11 or another edition by changing a few
values; the shipped configuration targets the **Windows 10 (multi-edition ISO)**.

> **Language:** [English](readme.md) | [Español](readme.es.md)
> **Technical guide:** [English](docs/TECHNICAL_GUIDE.md) | [Español](docs/TECHNICAL_GUIDE.es.md)

> **Windows 10 support status:** ordinary support ended on **October 14, 2025**;
> consumer **ESU** runs until **October 12, 2027** and commercial/educational ESU
> up to **October 10, 2028**. Windows Update will also stop working for
> **Windows 7 through Windows 10 without ESU** when the update service
> certificates are rotated (May/June 2027). See
> [Windows 10 end of support and extended security updates](#windows-10-end-of-support-and-extended-security-updates).

## Requirements

- **.NET 10** SDK (to build) or the .NET 10 runtime (to run a published build).
- Internet access to `microsoft.com` and `software.download.prss.microsoft.com`.
- **Linux only:** the system libraries required by the Playwright Firefox
  build. Install them with:
  ```bash
  ./bin/Release/net10.0/playwright install-deps firefox
  ```
  (needs `sudo`; on Debian/Ubuntu the script uses `apt-get`).

No manual browser installation is required: the tool downloads the matching
Playwright Firefox build the first time it runs.

## Building

```bash
dotnet build -c Release
```

The build output is placed in `bin/Release/net10.0/`.

The solution also ships an xUnit test project (download resume, configuration
parsing, CLI parsing, link selection, ISO validation, …):

```bash
dotnet test -c Release
```

To produce a self-contained executable for a specific platform:

```bash
# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true
```

### 32-bit (x86) and other architectures

The project is built as **AnyCPU** with `Prefer32Bit` disabled, so a single
managed assembly runs on 32-bit and 64-bit runtimes; on 64-bit Windows the
default process is 64-bit. Additional builds:

```bash
# 32-bit Windows (x86)
dotnet publish -c Release -r win-x86 --self-contained true -p:PlaywrightPlatform=win

# 64-bit ARM Linux
dotnet publish -c Release -r linux-arm64 --self-contained true
```

The `-p:PlaywrightPlatform=win` flag tells Playwright which driver to bundle: for
an unrecognized runtime identifier such as `win-x86` it would otherwise copy the
browser driver of **every** platform (about 575 MB).

> **Important:** the heavy lifting is done by the browser engine. Playwright only
> ships a **64-bit** driver (`win32_x64`, `linux-x64`, `linux-arm64`, macOS). A
> `win-x86` build therefore works on a 64-bit Windows installation (the 32-bit
> process starts the bundled 64-bit driver) but cannot run on a genuinely
> 32-bit-only Windows system. Likewise, .NET 10 has no 32-bit x86 Linux runtime
> (`linux-x86`), and Playwright has no 32-bit ARM driver, so a `linux-arm` build
> compiles but the browser cannot start. See the
> [technical guide](docs/TECHNICAL_GUIDE.md#81-32-bit-x86-and-arm-support) for
> the full matrix.

## Running

The tool reads `config.json` from the same folder as the executable (falling
back to the current working directory).

**Linux / macOS**

```bash
cd bin/Release/net10.0
./WindowsIsoDownloader
```

**Windows**

```bat
cd bin\Release\net10.0
WindowsIsoDownloader.exe
```

The ISO is written to the folder configured in `config.json` (the default is
the current working directory, producing `./windows10.iso`).

If the ISO already exists the tool stops immediately; pass `--force` to download
it again. Should the run be interrupted (network drop, `Ctrl+C`), the partial
data is kept in `windows10.iso.part` and the next run resumes from there.

### Command line options

| Option | Description |
| --- | --- |
| `-c`, `--config <path>` | Use a specific `config.json` file. |
| `-o`, `--output <path>` | Full path for the ISO (overrides `DownloadFolder`/`DownloadFilename`). |
| `-f`, `--force` | Overwrite an existing ISO. |
| `--dry-run` | Resolve the download link but do not download. |
| `--arch <x64\|x86\|arm64\|any>` | Preferred image architecture (default `x64`). |
| `--headed` | Show the browser window (useful to debug). |
| `--headless` | Force headless mode (default). |
| `--slow-mo <ms>` | Slow down browser actions by the given delay. |
| `--list-languages` | Print the languages available for the configured edition and exit. |
| `--experimental-sentinel` | Best-effort warm-up of Microsoft's anti-bot handshake. |
| `-v`, `--verbose` | Verbose logging. |
| `-h`, `--help` | Show the help text. |

```bash
# List the languages available for the configured edition
./WindowsIsoDownloader --list-languages

# Download the 32-bit image to a specific path
./WindowsIsoDownloader --arch x86 -o /home/user/isos/windows10.iso

# Only check that a download link can be obtained
./WindowsIsoDownloader --dry-run
```

## Configuration (`config.json`)

The whole workflow is data-driven so that, if Microsoft changes its page, you
only need to edit `config.json` instead of the source code.

| Field | Description |
| --- | --- |
| `DownloadFolder` | Folder where the ISO is saved. Both Windows (`C:\\isos`) and Linux (`/home/user/isos`) paths are accepted. A relative path (default `.`) is resolved against the working directory. |
| `DownloadFilename` | File name of the resulting ISO. It must be a plain name (no path separators). |
| `PreferredArchitecture` | Image to prefer when the page offers several: `X64` (default), `X86`, `Arm64` or `Any`. |
| `Attempts` | How many times the whole workflow is attempted (default `3`). |
| `RetryDelaySeconds` | Base delay between attempts, in seconds; it grows exponentially and is capped at 60 s (default `5`). |
| `Headless` | Run the browser without a visible window (default `true`). |
| `ExperimentalSentinel` | Enable the experimental anti-bot warm-up (default `false`). |
| `Actions` | Ordered list of browser actions used to reach the download link. |

Each action has:

| Field | Description |
| --- | --- |
| `Order` | Execution order (ascending). |
| `Kind` | One of `Goto`, `SelectOption`, `Click`, `QuerySelector`, `WaitForSelector`. |
| `WaitBeforeAction` | Optional delay in **milliseconds** before running the action. |
| `Optional` | When `true`, a failure (for example, the element is not present) is logged and the workflow continues (default `false`). |
| `Timeout` | Optional per-action timeout in milliseconds. |
| `Parameters` | Action specific parameters (`Url`, `Selector`, `Values`). |

Supported actions:

- **`Goto`** – navigates to `Parameters.Url`.
- **`SelectOption`** – selects `Parameters.Values` in the element matching
  `Parameters.Selector`.
- **`Click`** – clicks the element matching `Parameters.Selector`.
- **`QuerySelector`** – stores the element matching `Parameters.Selector`; the
  tool then reads its `href` to obtain the ISO link.
- **`WaitForSelector`** – waits until the element matching
  `Parameters.Selector` is attached to the DOM.

`config.json` declares `"$schema": "./config.schema.json"`, so editors with JSON
schema support offer completion and validation while you edit it.

### Updating the option values

Microsoft periodically changes the internal ids of the edition and the
language. You can find the current values directly on the page:

1. Open the [Windows 10 ISO download page](https://www.microsoft.com/en-us/software-download/windows10ISO).
2. Open the browser developer tools.
3. Edition id: inspect the `#product-edition` `<select>` element. The value of
   the "Windows 10 (multi-edition ISO)" option goes into the `SelectOption` action
   with `Order: 3`.
4. Language: choose the edition, click **Confirm**, then inspect the
   `#product-languages` `<select>`. Copy the whole `value` of your language
   (it looks like `{"id":"16067","language":"English"}`) into the `SelectOption`
   action with `Order: 5`.

The tool is resilient: after running the actions it also looks for any
`a[href*=".iso"]` link on the page, so small markup changes usually do not break
it. When Microsoft offers more than one image — Windows 10 publishes both a
**32-bit** and a **64-bit** ISO — the tool automatically picks the one matching
`PreferredArchitecture` (the **64-bit** image by default; use `--arch x86` for
the 32-bit one).

### Available languages

The **Windows 10 (multi-edition ISO)** edition (`2618`) offers the following 38
languages. To download one of them, replace the `Values` of the `SelectOption`
action with `Order: 5` in `config.json`:

| Language | `id` | `Values` (paste this) |
| --- | --- | --- |
| Arabic | `16058` | `{"id":"16058","language":"Arabic"}` |
| Brazilian Portuguese | `16059` | `{"id":"16059","language":"Brazilian Portuguese"}` |
| Bulgarian | `16060` | `{"id":"16060","language":"Bulgarian"}` |
| Chinese (Simplified) | `16061` | `{"id":"16061","language":"Chinese (Simplified)"}` |
| Chinese (Traditional) | `16062` | `{"id":"16062","language":"Chinese (Traditional)"}` |
| Croatian | `16063` | `{"id":"16063","language":"Croatian"}` |
| Czech | `16064` | `{"id":"16064","language":"Czech"}` |
| Danish | `16065` | `{"id":"16065","language":"Danish"}` |
| Dutch | `16066` | `{"id":"16066","language":"Dutch"}` |
| English | `16067` | `{"id":"16067","language":"English"}` |
| English International | `16068` | `{"id":"16068","language":"English International"}` |
| Estonian | `16069` | `{"id":"16069","language":"Estonian"}` |
| Finnish | `16070` | `{"id":"16070","language":"Finnish"}` |
| French | `16071` | `{"id":"16071","language":"French"}` |
| French Canadian | `16072` | `{"id":"16072","language":"French Canadian"}` |
| German | `16073` | `{"id":"16073","language":"German"}` |
| Greek | `16074` | `{"id":"16074","language":"Greek"}` |
| Hebrew | `16075` | `{"id":"16075","language":"Hebrew"}` |
| Hungarian | `16076` | `{"id":"16076","language":"Hungarian"}` |
| Italian | `16077` | `{"id":"16077","language":"Italian"}` |
| Japanese | `16078` | `{"id":"16078","language":"Japanese"}` |
| Korean | `16079` | `{"id":"16079","language":"Korean"}` |
| Latvian | `16080` | `{"id":"16080","language":"Latvian"}` |
| Lithuanian | `16081` | `{"id":"16081","language":"Lithuanian"}` |
| Norwegian | `16082` | `{"id":"16082","language":"Norwegian"}` |
| Polish | `16083` | `{"id":"16083","language":"Polish"}` |
| Portuguese | `16084` | `{"id":"16084","language":"Portuguese"}` |
| Romanian | `16085` | `{"id":"16085","language":"Romanian"}` |
| Russian | `16086` | `{"id":"16086","language":"Russian"}` |
| Serbian Latin | `16087` | `{"id":"16087","language":"Serbian Latin"}` |
| Slovak | `16088` | `{"id":"16088","language":"Slovak"}` |
| Slovenian | `16089` | `{"id":"16089","language":"Slovenian"}` |
| Spanish | `16090` | `{"id":"16090","language":"Spanish"}` |
| Spanish (Mexico) | `16091` | `{"id":"16091","language":"Spanish (Mexico)"}` |
| Swedish | `16092` | `{"id":"16092","language":"Swedish"}` |
| Thai | `16093` | `{"id":"16093","language":"Thai"}` |
| Turkish | `16094` | `{"id":"16094","language":"Turkish"}` |
| Ukrainian | `16095` | `{"id":"16095","language":"Ukrainian"}` |

> These `id` values belong to the Windows 10 edition `2618` (the `SelectOption`
> action with `Order: 3`).
> Languages you may miss (for example Indonesian, Malay, Persian or Vietnamese)
> are simply not offered for this edition. The most future-proof option is still
> to copy the whole `value` from the page as described above.

## Windows 10 end of support and extended security updates

Windows 10 reached **end of support on October 14, 2025**: it no longer receives
free security updates, and only the **Extended Security Updates (ESU)** program
keeps it patched.

| Stage | Ends | Applies to |
| --- | --- | --- |
| Ordinary support | **October 14, 2025** | Everyone |
| ESU – Year 1 | **October 13, 2026** | Commercial / educational |
| ESU – Year 2 | **October 12, 2027** | Consumers (end of consumer ESU); Year 2 for commercial |
| ESU – Year 3 | **October 10, 2028** | Commercial / educational only |

- **Consumers** can enrol in ESU — free in the European Economic Area, and with a
  Microsoft account / Microsoft Rewards points elsewhere — and are covered until
  **October 12, 2027**.
- **Commercial / educational** customers can buy up to **three years** of ESU:
  Year 1 ends October 13, 2026, Year 2 ends October 12, 2027 and Year 3 ends
  **October 10, 2028**.
- ESU contains **only** critical and important security updates; no new
  features, non-security fixes or design changes.

> **Warning — Windows Update certificate rotation.** The certificates that
> secure the connection to Windows Update expire in **2027** (their replacements
> must be installed **before May 17, 2027** or **before June 19, 2027**,
> depending on the product). Devices that never receive the replacement
> certificates — **Windows 7, Windows 8 / 8.1, any Windows 10 installation
> without ESU**, and the out-of-support Windows Server releases — will **lose
> access to Windows Update** and stop receiving updates altogether. This is a
> rotation of the service **root certificates** and is unrelated to this tool.
> Staying updated requires Windows 10 **with ESU** (installing the monthly
> update, at least the July 2026 one, before June 19, 2027) or a supported OS
> (Windows 11 24H2 or later, Windows Server 2022 or later, or the LTSC releases
> listed by Microsoft).
>
> Official announcement:
> <https://techcommunity.microsoft.com/blog/windows-itpro-blog/prepare-for-windows-update-certificate-rotation-in-2027/4562463>

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success (ISO downloaded, or a dry run / language listing finished). |
| `1` | Unexpected error (bad command line). |
| `2` | Configuration or page-layout error (no download link found). |
| `3` | Network error while downloading. |
| `4` | Anti-bot rejection (Sentinel). |
| `5` | Disk error (permissions, I/O). |
| `6` | Browser error (Firefox could not start). |
| `130` | Cancelled with `Ctrl+C`. |

## Troubleshooting

- **`Could not launch Firefox` on Linux** – install the Playwright system
  dependencies: `playwright install-deps firefox` (see *Requirements*).
- **`Download button was not found` / anti-bot** – Microsoft protects the
  download connector with a mechanism called *Sentinel* that may reject a
  browser session, especially under heavy or automated use. The tool retries up
  to `Attempts` times (default 3) with a fresh browser session and a growing
  delay; if it still fails, wait a few minutes and try again. Running with
  `--headed` and `--verbose` shows what the page is doing.
- **The download was interrupted** – nothing is lost: the partial data stays in
  `*.part` and the next run resumes it with an HTTP `Range` request. Delete the
  `.part` file to start over.
- **Permission errors** – make sure `DownloadFolder` is writable. On Windows
  run the terminal as administrator; on Linux check the folder permissions.
- **The page changed** – use `--dry-run --headed` to watch the automation, save a
  diagnostic screenshot/HTML (written to `diagnostics/` when a run fails) and
  adjust `config.json` accordingly.

## License

See [LICENSE](LICENSE).




## Windows Update shutdown — practical notes

> Personal notes collected by the maintainer on October 12, 2026. They
> complement the official summary above with practical workarounds and their
> trade-offs; they are not official Microsoft guidance.

ATTENTION, NOTICE (October 12, 2026): microsoft will cut off starting June 19,
2027 the access to windows update for all the computers that have windows 10
without ESU and below (the previous block was up to windows vista-server 2008
without R2), this logically affects the server versions (server 2008 R2 up to
server 2019, from server 2022 onwards, the windows update closure doesn't
affect it), this is due to the expiry of the root certificates of the update
service that expire as of this date, if you want to take a look at the official
link of the microsoft news, here it is (use the browser translation, it's in
English):

https://techcommunity.microsoft.com/blog/windows-itpro-blog/prepare-for-windows-update-certificate-rotation-in-2027/4562463

remember that root certificates are part of internet browsing, one of the
characteristics of the https protocol is that now everything that is transferred
to the router is encrypted (previously it wasn't so), but for programs used to
analyze everything that is transferred on a home network like wireshark to have
no effect (no sensitive data is seen), the root certificates must work and be
up to date, if they don't work, it gives a certificate error in the browser and
the programs that connect to the internet will stop working, you have to take
this into account when using an old windows installed on the computer (by the
way, the windows update closure also affects virtual machines, i.e., the PCs
booted inside a physical machine).

when the expiry date of the root certificate arrives it's possible that the
offices installed on windows without support give errors checking updates (up
to 10 and server 2019 which are client and server respectively you can install
the 2021-365, 7 and 8, up to office 2016), this being solved by deactivating
the office updates (when the expiry date of said root certificates arrives).

in the previous window (automatic updates were imposed starting from windows
2000) it didn't give any kind of failure when booting the operating system,
only when opening the microsoft update page or starting the windows update.
(I experienced the previous update closure window).

in case it's windows 10 with ESU, windows 11 24H2, LTSC servers of 2019 and
2016 and server 2022, microsoft has put as the official method to solve the
problem, the update, i.e., you'll have to update the second Tuesday of each
month until the date of June 19, 2027 in which already...piano, piano (you'll
understand what I'm telling you).

in case you come across this type of computers, these are the paths you have:

-  buy the ESU (windows 10) (in the case of countries of the european union, it has no cost)
-  patch the update to access the ESU, the only patch (and recommended for windows 10) is the MAS (microsoft activation script)
-  install an unofficial patch to update the root certificates, which is the legacy update
-  in the case of compatible versions (it's not mandatory for now from 11 24H2, windows 10 without ESU is essential
half-way) accept the updates to major versions of windows via update or doing an update from the windows installation
media.
-  keep the windows installation and do a dual boot with linux (not advisable).
-  uninstall windows and install linux together with a windows virtual machine (there everything is allowed, from originals to modified ones)

in case you choose the sensible option (the last one), remember that you must
set up vmware or virtualbox on linux (install linux on the computer logically)
and the virtual machine with the windows of your choice (that there you can
indeed install old windows) in an official way until June 2027, afterwards it
won't work and you'll only be able to install and update this type of PCs by
unofficial methods.

in case you've lost your job or the course for having told you this
inconvenience, good luck in your job search or in the course search (which I
hope you find), with all these clues I tell you you'll see how surely in the
end you find the dreamed job or course.
