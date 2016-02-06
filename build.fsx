
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
    if Fake.FileHelper.TestFile exeFile
    then CleanDir @".\nuget"
         CopyFiles @".\nuget" [ exeFile ]
         NuGet (fun p -> 
        {p with
            Authors = [ "Frank Pfattheicher" ]
            Project = "RefCheck"
            Description = "VisualStudio solution reference checking tool"
            OutputPath = @".\nuget"
            Summary = "Public Developer Tools"
            WorkingDir = @".\nuget"
            Version = "1.0.0"
            Files = [("RefCheck.exe", None, None);(@"*.dll", None, None)]
            Publish = false }) 
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
