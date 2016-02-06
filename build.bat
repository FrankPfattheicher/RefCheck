@echo off
cls

'Get FAKE
IF NOT EXIST "packages\FAKE\tools\Fake.exe" ".\NuGet.exe" "Install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"

'Eat your own dogfood
IF NOT EXIST "packages\RefCheck\tools\RefCheck.exe" ".\NuGet.exe" "Install" "RefCheck" "-OutputDirectory" "packages" "-ExcludeVersion"

"packages\FAKE\tools\Fake.exe" build.fsx

