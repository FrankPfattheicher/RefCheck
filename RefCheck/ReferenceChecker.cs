using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace RefCheck;

public class ReferenceChecker
{
    private readonly Dictionary<string, string> _frameworkAssemblies = new();
    private bool _checkDone;

    public readonly List<Solution> Solutions = new();
    public readonly List<Project> Projects = new();
    public readonly List<NugetPackage> NugetPackages = new();

    public event Action<string>? Processing;
    public event Action<string>? Error;
    public event Action<string>? Warning;


    public List<string> Errors => Solutions.SelectMany(s => s.Errors).ToList();
    public List<string> Warnings => Solutions.SelectMany(s => s.Warnings).ToList();
    

    public ReferenceChecker()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
#pragma warning disable CA1416
            var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NETFramework", false);
            if (regKey == null)
                return;

            var installRoot = regKey.GetValue("InstallRoot", "")?.ToString() ?? "";
            var frameworkDir = Directory.EnumerateDirectories(installRoot, "v?.*").OrderByDescending(Path.GetFileName)
                .First();
            AddFrameworkDlls(frameworkDir);

            var visualStudioToolsDir = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Where(kv => kv.Key.ToString()!.StartsWith("VS") && kv.Key.ToString()!.Contains("COMNTOOLS"))
                .OrderByDescending(kv => kv.Key)
                .Select(kv => kv.Value)
                .FirstOrDefault()
                ?.ToString();
            if (visualStudioToolsDir != null)
            {
                var visualStudioCommonDir = Path.GetDirectoryName(Path.GetDirectoryName(visualStudioToolsDir));
                if (visualStudioCommonDir != null) AddFrameworkDlls(visualStudioCommonDir);
            }
#pragma warning restore CA1416
        }
        //
        // Projects = solution.Projects
        //     .SelectMany(p => p.ProjectReferences)
        //     .OrderBy(r => r.ToString())
        //     .ToList();
        //
        // NugetReferences = solution.Projects
        //     .SelectMany(p => p.NugetReferences)
        //     .OrderBy(r => r.ToString())
        //     .ToList();
    }

    private void AddFrameworkDlls(string path)
    {
        var frameworkDlls = Directory.EnumerateFileSystemEntries(path, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in frameworkDlls)
        {
            var name = Path.GetFileNameWithoutExtension(dll).ToLower();
            _frameworkAssemblies.TryAdd(name, dll);
        }
    }

    public void CheckSolutions(List<string> solutionFileNames)
    {
        foreach (var fileName in solutionFileNames)
        {
            var solution = new Solution(fileName);
            solution.Load(this);
            Console.WriteLine(@"Solution: " + solution.FileName);
            foreach (var error in solution.Errors)
            {
                Error?.Invoke(error);
            }
            Solutions.Add(solution);

            CheckSolution(solution);
        }

        _checkDone = true;
    }

    private void CheckSolution(Solution solution)
    {
        Processing?.Invoke($"Using settings from {solution.RefSettings.FileName}");

        Processing?.Invoke($"Checking {solution.Projects.Count} projects");

        foreach (var project in solution.Projects)
        {
            CheckProject(project);
        }

        if (Projects.Count > 0)
        {
            CheckForMixedProjects(solution);
            CheckForBlacklistedProjects(solution);
        }

        if (NugetPackages.Count > 0)
        {
            CheckForMixedNugetReferences(solution);
            CheckForBlacklistedNugetReferences(solution);
            CheckForImplicitNugetReferences(solution);
        }
    }

    private void CheckProject(Project project)
    {
        Processing?.Invoke($"Checking {project.ShortName} {project.ProjectReferences.Count} project references");
        foreach (var reference in project.ProjectReferences)
        {
            CheckProjectReference(reference);
        }

        Processing?.Invoke($"Checking {project.ShortName} {project.NugetReferences.Count} Nuget references");
        foreach (var nugetReference in project.NugetReferences)
        {
            CheckNugetReference(project, nugetReference);
        }
    }

    private void CheckProjectReference(Project project)
    {
        project.IsWhitelisted = project.Solution.RefSettings["Whitelisted"].Get(project.IniKey, false);
        project.IsBlacklisted = project.Solution.RefSettings["Blacklisted"].Get(project.IniKey, false);

        if (!string.IsNullOrEmpty(project.SourcePath))
        {
            project.IsPresent = File.Exists(project.SourcePath);
            return;
        }

        if (!_frameworkAssemblies.ContainsKey(project.ProjectFileName.ToLower()))
        {
            project.IsPresent = false;
            return;
        }

        var path = _frameworkAssemblies[project.ProjectFileName.ToLower()];
        project.IsPresent = File.Exists(path);

        if (project.IsPresent && string.IsNullOrEmpty(project.SourcePath))
        {
            project.SourcePath = path;
        }
    }

    private void CheckNugetReference(Project project, NugetPackage nugetPackage)
    {
        Processing?.Invoke($"[NUGET] {nugetPackage.Name} {nugetPackage.Version}");

        nugetPackage.IsWhitelisted = project.Solution.RefSettings["Whitelisted"].Get(nugetPackage.IniKey, false);
        nugetPackage.IsBlacklisted = project.Solution.RefSettings["Blacklisted"].Get(nugetPackage.IniKey, false);

        // if (!string.IsNullOrEmpty(nugetPackage.SourcePath))
        // {
        //     nugetPackage.IsPresent = File.Exists(nugetPackage.SourcePath);
        //     return;
        // }

        if (!_frameworkAssemblies.ContainsKey(nugetPackage.Name.ToLower()))
        {
            nugetPackage.IsPresent = false;
            return;
        }

        var path = _frameworkAssemblies[nugetPackage.Name.ToLower()];
        nugetPackage.IsPresent = File.Exists(path);

        // if (nugetPackage.IsPresent && string.IsNullOrEmpty(nugetPackage.SourcePath))
        // {
        //     nugetPackage.SourcePath = path;
        // }
    }

    private void CheckForMixedProjects(Solution solution)
    {
        Processing?.Invoke("Check for mixed project references..");

        var refGroups = Projects.GroupBy(r => r.RefId)
            .Select(g => g.AsQueryable().ToList())
            .Where(rl => rl.Count > 1)
            .ToList();

        var warningRefs = refGroups.Select(g => g.GroupBy(r => r.SourceId).ToList())
            .Where(rg => rg.Count > 1)
            .ToList();

        foreach (var warningRef in warningRefs)
        {
            var warnRef = warningRef.First().AsEnumerable().First();
            var versions = string.Join(" <-> ",
                warningRef.SelectMany(w => w.AsEnumerable())
                    .Select(r => r.SourceVersion).Distinct()
            );

            var warning = $"{warnRef.ProjectFileName} {warnRef.Version}, used: {versions}";
            solution.Warnings.Add(warning);
            Warning?.Invoke(warning);
        }

        var referenced = warningRefs
            .SelectMany(g => g.AsEnumerable())
            .Select(g => g.Key)
            .ToList();

        foreach (var reference in Projects)
        {
            reference.IsWarning = referenced.Contains(reference.SourceId);
        }
    }

    private void CheckForBlacklistedProjects(Solution solution)
    {
        Processing?.Invoke("Check for blacklisted Project references..");

        foreach (var reference in Projects.Where(r => !r.IsWhitelisted))
        {
            if (reference.IsBlacklisted)
            {
                var error = $"Blacklisted: {reference.ProjectFileName} {reference.Version}";
                if (!solution.Errors.Contains(error))
                {
                    solution.Errors.Add(error);
                    Error?.Invoke(error);
                }
            }
        }
    }

    private void CheckForMixedNugetReferences(Solution solution)
    {
        Processing?.Invoke("Check for mixed Nuget references..");

        var refGroups = NugetPackages.GroupBy(r => r.Name)
            .Select(g => g.AsQueryable().ToList())
            .Where(rl => rl.Count > 1)
            .ToList();

        var warningRefs = refGroups.Select(g => g.GroupBy(r => r.RefId).ToList())
            .Where(rg => rg.Count > 1)
            .ToList();

        foreach (var warningRef in warningRefs)
        {
            var warnRef = warningRef.First().AsEnumerable().First();
            var versions = string.Join(" <-> ",
                warningRef.SelectMany(w => w.AsEnumerable())
                    .Select(r => r.Version).Distinct()
            );

            var warning = $"{warnRef.Name} {warnRef.Version}, used: {versions}";
            solution.Warnings.Add(warning);
            Warning?.Invoke(warning);
        }

        var referenced = warningRefs
            .SelectMany(g => g.AsEnumerable())
            .Select(g => g.Key)
            .ToList();

        foreach (var reference in Projects)
        {
            reference.IsWarning = referenced.Contains(reference.SourceId);
        }
    }

    private void CheckForBlacklistedNugetReferences(Solution solution)
    {
        Processing?.Invoke("Check for blacklisted Nuget references..");

        foreach (var reference in NugetPackages.Where(r => !r.IsWhitelisted))
        {
            if (reference.IsBlacklisted)
            {
                var error = $"Blacklisted: {reference.Name} {reference.Version}";
                if (!solution.Errors.Contains(error))
                {
                    solution.Errors.Add(error);
                    Error?.Invoke(error);
                }
            }
        }
    }

    private void CheckForImplicitNugetReferences(Solution solution)
    {
        Processing?.Invoke("Check for implicit Nuget references..");

        foreach (var nugetReference in NugetPackages)
        {
            var projects = solution.Projects
                .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
            foreach (var project in projects)
            {
                if (project.IsImplicitNugetReference(nugetReference))
                {
                    var warning =
                        $"Unnecessary reference: Project {project.ShortName} -> {nugetReference.Name} {nugetReference.Version}";
                    if (!solution.Errors.Contains(warning))
                    {
                        solution.Warnings.Add(warning);
                        Warning?.Invoke(warning);
                    }
                }
            }
        }
    }

    public string CheckResult
    {
        get
        {
            if (!_checkDone) return string.Empty;

            var result = string.Empty;
            switch (Errors.Count)
            {
                case 0:
                    result += "No errors";
                    break;
                case 1:
                    result += "1 error";
                    break;
                default:
                    result += $"{Errors.Count} errors";
                    break;
            }

            result += ", ";
            switch (Warnings.Count)
            {
                case 0:
                    result += "no warnings";
                    break;
                case 1:
                    result += "1 warning";
                    break;
                default:
                    result += $"{Warnings.Count} warnings";
                    break;
            }

            return result;
        }
    }

    // public void Save()
    // {
    //     foreach (var project in _solution.Projects)
    //     {
    //         foreach (var reference in project.ProjectReferences)
    //         {
    //             var isWhitelisted = _refSettings["Whitelisted"].Get(reference.IniKey, false);
    //             var isBlacklisted = _refSettings["Blacklisted"].Get(reference.IniKey, false);
    //
    //             if (reference.IsWhitelisted != isWhitelisted)
    //             {
    //                 _refSettings["Whitelisted"].Set(reference.IniKey, reference.IsWhitelisted);
    //             }
    //
    //             if (reference.IsBlacklisted != isBlacklisted)
    //             {
    //                 _refSettings["Blacklisted"].Set(reference.IniKey, reference.IsBlacklisted);
    //             }
    //         }
    //     }
    //
    //     _refSettings.Save();
    // }
    
    
}