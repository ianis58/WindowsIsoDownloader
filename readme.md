# Windows Iso Downloader

Windows Iso Downloader is a tool that allows to programatically download the latest Windows 11 ISO file from Microsoft servers.
It can be usefull in projects that allows you to build a custom installer for Windows 11, to reduce manual actions.

## Building
You'll need the dotnet 6 SDK installed to build it.
Then run:
```
dotnet build
```

## Run it
1. Build it with above instructions or download it from the releases section.
2. Open a terminal as administrator
3. cd to the right folder then run:
```
./WindowsIsoDownloader.exe
```
4. Sit back and relax. 😎 The result will be a windows11.iso file in C:\windows11.iso
