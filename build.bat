@echo off
cls
IF NOT EXIST "packages\FAKE\tools\Fake.exe" ".\NuGet.exe" "Install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"
"packages\FAKE\tools\Fake.exe" build.fsx

