@echo off
cd %~dp0

IF EXIST .nuget\NuGet.exe goto part2
echo Downloading latest version of NuGet.exe...
@powershell -NoProfile -ExecutionPolicy unrestricted -Command ".\build\downloadnuget.ps1"

:part2
set EnableNuGetPackageRestore=true
.nuget\NuGet.exe install Sake -version 0.2 -o packages -source https://api.nuget.org/v3/index.json
packages\Sake.0.2\tools\Sake.exe -I build -f Sakefile.shade %*
