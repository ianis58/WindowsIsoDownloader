# Windows Iso Downloader

Windows Iso Downloader is a tool that allows to programatically download the latest Windows 11 ISO file from Microsoft servers.
It can be usefull in projects that allows you to build a custom installer for Windows 11, to reduce manual actions.

## Building
1. Build it with the "dotnet build" command
2. Run the produced WindowsIsoDownloader.exe
3. The result will be a windows11.iso file in the same folder as WindowsIsoDownloader.exe

## Why not publishing a release?
I prefer not to publish a pre-built executable. The goal is to have as little code as possible,
so that it's very easy to audit it by reading the sources.
