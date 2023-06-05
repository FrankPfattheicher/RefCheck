﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using IctBaden.Framework.FileSystem;

namespace RefCheck;

public class Project
{
    private readonly Solution _solution;
    private bool _isWhitelisted;
    private bool _isBlacklisted;

    public bool IsPresent { get; set; }
    public bool IsWarning { get; set; }

    public bool IsWhitelisted
    {
        get => _isWhitelisted;
        set
        {
            _isWhitelisted = value;
            if (_isWhitelisted)
            {
                _isBlacklisted = false;
            }
        }
    }

    public bool IsBlacklisted
    {
        get => _isBlacklisted;
        set
        {
            _isBlacklisted = value;
            if (_isBlacklisted)
            {
                _isWhitelisted = false;
            }
        }
    }

    public string RefId => $"{ProjectFileName}_{Version}";
    public string SourceId => $"{ProjectFileName}_{Version}_{SourceVersion}";

    public string Version { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;


    public string IniKey
    {
        get
        {
            var key = ProjectFileName;
            if (!string.IsNullOrEmpty(Version))
            {
                key += "_" + Version;
            }
            return key;
        }
    }

    public override string ToString()
    {
        var txt = ProjectFileName;
        if (!string.IsNullOrEmpty(Version))
        {
            txt += " V" + Version;
        }
        return txt;
    }


    public Project(Solution solution, string projectFileName)
    {
        _solution = solution;
        ProjectFileName = projectFileName;
    }

    public string SolutionName => _solution.Name;
    public string ProjectFileName { get; private set; }
    
    public string ShortName => Path.GetFileNameWithoutExtension(ProjectFileName);
    
    public string RelativeName => FileSystemNaming
        .GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(SolutionName)) ?? ".", ProjectFileName);

    public readonly List<Project> ProjectReferences = new();
    public readonly List<NugetPackage> NugetReferences = new();

    public bool IsImplicitNugetReference(NugetPackage nuRef)
    {
        foreach (var project in ProjectReferences)
        {
            if (project.IsImplicitNugetReference(nuRef)) return true;
            if (!project.NugetReferences.Any()) return false;
            if (project.NugetReferences.Any(n => n.RefId == nuRef.RefId)) return true;
        }
        return false;
    }

    public bool Load()
    {
        if (!File.Exists(ProjectFileName))
        {
            return false;
        }

        var xml = new XmlDocument();
        xml.Load(ProjectFileName);

        var path = Path.GetDirectoryName(ProjectFileName) ?? ".";

        
        var nugetReferences = xml
            .SelectNodes("//*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='PackageReference']")
            ?.GetEnumerator();

        while (nugetReferences != null && nugetReferences.MoveNext())
        {
            if (nugetReferences.Current is not XmlNode reference) continue;
            
            var name = reference.Attributes?["Include"]?.Value;
            var version = reference.Attributes?["Version"]?.Value;
            if (name == null || version == null) continue;
            
            var nugetRef = new NugetPackage(name, version);
            NugetReferences.Add(nugetRef);
        }


        var projects = xml
            .SelectNodes("//*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='ProjectReference']")
            ?.GetEnumerator();

        while (projects != null && projects.MoveNext())
        {
            if (projects.Current is not XmlNode reference) continue;

            var name = reference.Attributes?["Include"]?.Value.Split(',');
            if(name == null) continue;

            var basePath = Path.GetDirectoryName(ProjectFileName) ?? ".";
            var fileName = Path.GetFullPath(name[0].Trim(), basePath);
            var project = _solution.Projects.FirstOrDefault(p => p.ProjectFileName == fileName)
                ?? new Project(_solution, fileName);
            project.Load();
            
            if (name.Length > 1)
            {
                var options = name.Select(str => str.Split('='))
                    .Where(pair => pair.Length == 2)
                    .ToDictionary(pair => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pair[0].Trim()), pair => pair[1].Trim());

                foreach (var option in options)
                {
                    var pi = project.GetType().GetProperty(option.Key);
                    pi?.SetValue(project, option.Value);
                }
            }

            var hintPath = reference.SelectSingleNode("*[local-name()='HintPath']");
            if (hintPath != null)
            {
                project.SourcePath = Path.GetFullPath(Path.Combine(path, hintPath.InnerText));
                project.IsPresent = File.Exists(project.SourcePath);
                if (project.IsPresent)
                {
                    var fvi = FileVersionInfo.GetVersionInfo(project.SourcePath);
                    if(fvi.ProductVersion != null)
                    {
                        project.SourceVersion = fvi.ProductVersion
                        .Replace(fvi.ProductPrivatePart.ToString(), "")
                        .Trim('.');
                    }
                }
            }
            
            ProjectReferences.Add(project);
        }

        return true;
    }
}