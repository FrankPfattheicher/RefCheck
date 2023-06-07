using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IctBaden.Framework.IniFile;
using Microsoft.Win32;

namespace RefCheck;

public class ReferenceChecker
{
    private readonly Solution _solution;
    private readonly Profile _refSettings;
    private readonly Dictionary<string, string> _frameworkAssemblies = new();
    private bool _checkDone;

    public readonly List<Project> Projects = new();
    private readonly List<NugetPackage> _nugetReferences = new();

    public event Action<string>? Processing;
    public event Action<string>? Error;
    public event Action<string>? Warning;

    public ReferenceChecker(Solution solution, Profile refSettings)
    {
        _solution = solution;
        _refSettings = refSettings;

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

        Projects = solution.Projects
            .SelectMany(p => p.ProjectReferences)
            .OrderBy(r => r.ToString())
            .ToList();

        _nugetReferences = solution.Projects
            .SelectMany(p => p.NugetReferences)
            .OrderBy(r => r.ToString())
            .ToList();
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

    public void Check()
    {
        foreach (var project in _solution.Projects)
        {
            Check(project);
        }

        if (Projects.Count > 0)
        {
            CheckForMixedProjects();
            CheckForBlacklistedProjects();
        }

        if (_nugetReferences.Count > 0)
        {
            CheckForMixedNugetReferences();
            CheckForBlacklistedNugetReferences();
            CheckForImplicitNugetReferences();
        }

        _checkDone = true;
    }

    private void Check(Project project)
    {
        Processing?.Invoke(project.RelativeName);
        foreach (var reference in project.ProjectReferences)
        {
            CheckProject(reference);
        }

        foreach (var nugetReference in project.NugetReferences)
        {
            CheckNugetReference(nugetReference);
        }
    }

    private void CheckProject(Project project)
    {
        project.IsWhitelisted = _refSettings["Whitelisted"].Get(project.IniKey, false);
        project.IsBlacklisted = _refSettings["Blacklisted"].Get(project.IniKey, false);

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

    private void CheckNugetReference(NugetPackage nugetPackage)
    {
        Processing?.Invoke($"[NUGET] {nugetPackage.Name} {nugetPackage.Version}");

        nugetPackage.IsWhitelisted = _refSettings["Whitelisted"].Get(nugetPackage.IniKey, false);
        nugetPackage.IsBlacklisted = _refSettings["Blacklisted"].Get(nugetPackage.IniKey, false);

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

    private void CheckForMixedProjects()
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
            _solution.Warnings.Add(warning);
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

    private void CheckForBlacklistedProjects()
    {
        Processing?.Invoke("Check for blacklisted Project references..");

        foreach (var reference in Projects.Where(r => !r.IsWhitelisted))
        {
            if (reference.IsBlacklisted)
            {
                var error = $"Blacklisted: {reference.ProjectFileName} {reference.Version}";
                if (!_solution.Errors.Contains(error))
                {
                    _solution.Errors.Add(error);
                    Error?.Invoke(error);
                }
            }
        }
    }

    private void CheckForMixedNugetReferences()
    {
        Processing?.Invoke("Check for mixed Nuget references..");

        var refGroups = _nugetReferences.GroupBy(r => r.Name)
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
            _solution.Warnings.Add(warning);
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

    private void CheckForBlacklistedNugetReferences()
    {
        Processing?.Invoke("Check for blacklisted Nuget references..");

        foreach (var reference in _nugetReferences.Where(r => !r.IsWhitelisted))
        {
            if (reference.IsBlacklisted)
            {
                var error = $"Blacklisted: {reference.Name} {reference.Version}";
                if (!_solution.Errors.Contains(error))
                {
                    _solution.Errors.Add(error);
                    Error?.Invoke(error);
                }
            }
        }
    }

    private void CheckForImplicitNugetReferences()
    {
        Processing?.Invoke("Check for implicit Nuget references..");

        foreach (var nugetReference in _nugetReferences)
        {
            var projects = _solution.Projects
                .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
            foreach (var project in projects)
            {
                if (project.IsImplicitNugetReference(nugetReference))
                {
                    var warning =
                        $"Unnecessary reference: Project {project.ShortName} -> {nugetReference.Name} {nugetReference.Version}";
                    if (!_solution.Errors.Contains(warning))
                    {
                        _solution.Warnings.Add(warning);
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
            switch (_solution.Errors.Count)
            {
                case 0:
                    result += "No errors";
                    break;
                case 1:
                    result += "1 error";
                    break;
                default:
                    result += $"{_solution.Errors.Count} errors";
                    break;
            }

            result += ", ";
            switch (_solution.Warnings.Count)
            {
                case 0:
                    result += "no warnings";
                    break;
                case 1:
                    result += "1 warning";
                    break;
                default:
                    result += $"{_solution.Warnings.Count} warnings";
                    break;
            }

            return result;
        }
    }

    public void Save()
    {
        foreach (var project in _solution.Projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                var isWhitelisted = _refSettings["Whitelisted"].Get(reference.IniKey, false);
                var isBlacklisted = _refSettings["Blacklisted"].Get(reference.IniKey, false);

                if (reference.IsWhitelisted != isWhitelisted)
                {
                    _refSettings["Whitelisted"].Set(reference.IniKey, reference.IsWhitelisted);
                }

                if (reference.IsBlacklisted != isBlacklisted)
                {
                    _refSettings["Blacklisted"].Set(reference.IniKey, reference.IsBlacklisted);
                }
            }
        }

        _refSettings.Save();
    }
}