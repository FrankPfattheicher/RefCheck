
#r @"packages\FAKE\tools\FakeLib.dll"
open Fake
open Fake.MSTest

// Properties
let buildDir = @".\RefCheck\RefCheck\bin\Release\"
let testsDir = @".\RefCheck\RefCheck.Tests\bin\Release\"
let artifactsDir = @".\TestResults"

Target "CreatePackage" (fun _ ->
    // Copy all the package files into a package folder
    let exeFile = @".\RefCheck\bin\Release\RefCheck.exe"
    let libFile1 = @".\RefCheck\bin\Release\IctBaden.Framework.dll"
    let libFile2 = @".\RefCheck\bin\Release\IctBaden.Presentation.dll"
    let nugetAccessKey = getBuildParamOrDefault "nugetkey" ""

    if Fake.FileHelper.TestFile exeFile
    then CleanDir @".\nuget\tools"
         CopyFiles @".\nuget\tools" [ exeFile; libFile1; libFile2 ]
         NuGet (fun p -> 
        {p with
            Authors = [ "Frank Pfattheicher" ]
            Project = "RefCheck"
            Description = "RefCheck - is a Nuget reference checking tool for VisualStudio soultions. This package bundles the tool to be run as a pre-build step."
            Summary = "RefCheck - VisualStudio references checking tool."
            OutputPath = @".\nuget"
            WorkingDir = @".\nuget"
            Version = "1.0.0.1"
            Files = [("tools\RefCheck.exe", Some @"tools", None);(@"tools\*.dll", Some @"tools", None)]
            AccessKey = nugetAccessKey
            Publish = true }) 
            @"RefCheck.nuspec"
    else
        printfn "*****************************************************" 
        printfn "Output file missing. Package built with RELEASE only." 
        printfn "*****************************************************" 
)

Target "BuildExe" (fun _ ->
     !! @".\**\*.RefCheck.csproj"   
      |> MSBuildRelease buildDir "Build"
      |> Log "AppBuild-Output: "
)

Target "BuildTests" (fun _ ->
     !! @".\**\*Tests.csproj"   
      |> MSBuildRelease testsDir "Build"
      |> Log "AppBuild-Output: "
)

Target "RunTests" (fun _ ->
    !! (testsDir + @"\*.Tests.dll") 
      |> MSTest (fun p -> {p with ResultsDir = artifactsDir })
)

// Dependencies
"BuildExe"
  ==> "BuildTests"
  ==> "RunTests"
  ==> "CreatePackage"

RunTargetOrDefault "CreatePackage"
