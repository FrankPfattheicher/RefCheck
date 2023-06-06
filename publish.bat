@echo off
set Version=
for /F "tokens=1,2" %%a in (ReleaseNotes.md) do ( 
    if NOT defined Version set Version=%%b 
)
echo ==================
echo  Version: %Version%
echo ==================
echo on

dotnet publish RefCheck -c Release -f net6.0 -r win-x64 --self-contained -p:AssemblyVersion=%Version% -p:Version=%Version% -p:VersionPrefix=%Version% -p:PublishSingleFile=true -p:PublishTrimmed=true

