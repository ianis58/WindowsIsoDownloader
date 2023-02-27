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

## License
MIT License

Copyright (c) 2023 ianis58

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
