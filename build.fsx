#r @"packages/tools/FAKE/tools/FakeLib.dll"
#I @"packages/tools/FSharpLint.Fake/tools"
#r @"packages/tools/FSharpLint.Fake/tools/FSharpLint.Fake.dll"

open System
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git
open Fake.Testing
open Fake.ReleaseNotesHelper
open FSharpLint.Fake

let project = "FSharpLint"

let release = LoadReleaseNotes "RELEASE_NOTES.md"

let genAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName
    let fileName = basePath + "/AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description "Lint tool for F#."
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ]

Target "AssemblyInfo" (fun _ ->
    !! "src/**/*.fsproj" |> Seq.iter genAssemblyInfo)

Target "RestorePackages" RestorePackages

Target "Clean" (fun _ -> CleanDirs ["bin"])

Target "Build" (fun _ ->
    !! "FSharpLint.sln"
    |> MSBuildRelease "" "Rebuild"
    |> ignore)

Target "RunTests" (fun _ ->
    !! "tests/**/bin/Release/*Tests*.dll" 
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            TimeOut = TimeSpan.FromMinutes 20.
            Where = "cat != Performance" }))

Target "RunFunctionalTests" (fun _ ->
    !! "tests/**/bin/Release/*FunctionalTest*.dll" 
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            TimeOut = TimeSpan.FromMinutes 20.
            Where = "cat != Performance" }))

Target "Package" (fun _ ->    
    Paket.Pack (fun p -> 
        { p with 
            ToolPath = ".paket/paket.exe" 
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes
            IncludeReferencedProjects = true
            OutputPath = "packaging" }))

Target "PublishPackages" (fun _ ->
    Paket.Push(fun p -> { p with WorkingDir = "packaging" }))

Target "Release" (fun _ ->
    StageAll ""
    Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion)

Target "Lint" (fun _ ->
    !! "src/**/*.fsproj" |> Seq.iter (FSharpLint id))

Target "GenerateDocs" (fun _ ->
    executeFSI "docs/tools" "generate.fsx" [] |> ignore)

Target "Default" DoNothing

"Clean" 
    ==> "RestorePackages"
    ==> "AssemblyInfo" 
    ==> "Build" 
    ==> "RunFunctionalTests" 
    ==> "RunTests"
    ==> "Lint" 
    ==> "Package" 
    ==> "Default"
    ==> "GenerateDocs" 
    ==> "PublishPackages" 
    ==> "Release"

RunTargetOrDefault "Default"