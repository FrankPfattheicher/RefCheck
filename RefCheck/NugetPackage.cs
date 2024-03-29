﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace RefCheck;

public class NugetPackage
{
    private readonly int _depth;
    public string Name { get; }
    public string Version { get; }

    private bool _isWhitelisted;
    private bool _isBlacklisted;
    public bool IsPresent { get; set; } = true;
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

    public string IniKey
    {
        get
        {
            var key = Name;
            if (!string.IsNullOrEmpty(Version))
            {
                key += "_" + Version;
            }
            return key;
        }
    }

    public string RefId => $"{Name.Replace("-", "_")}_{Version.Replace("-", "_")}";

    public override string ToString()
    {
        var txt = Name;
        if (!string.IsNullOrEmpty(Version))
        {
            txt += " V" + Version;
        }
        return txt;
    }

    public string Color
    {
        get
        {
            if (IsSystemPackage) return "#FFFFFF";
            if (IsMicrosoftPackage) return "#C0F0C0";
            return "#C0C0C0";
        }
    }

    public bool IsSystemPackage => Name.StartsWith("System", StringComparison.InvariantCultureIgnoreCase);
    public bool IsMicrosoftPackage => Name.StartsWith("Microsoft", StringComparison.InvariantCultureIgnoreCase);

    
    public readonly List<NugetPackage> References = new();

    public NugetPackage? RefFrom { get; init; }


    public NugetPackage(string name, string version, int depth)
    {
        _depth = depth;
        Name = name;
        Version = version
            .Replace("[", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("]", "")
            .Split(',', StringSplitOptions.TrimEntries)
            .Last();
    }

    public bool IsImplicitNugetReference(NugetPackage nuRef)
    {
        foreach (var nugetReference in References)
        {
            if (References.Any(n => n.Name == nuRef.Name)) return true;
            if (nugetReference.IsImplicitNugetReference(nuRef)) return true;
        }
        return false;
    }

    
    public void LoadReferences(ReferenceChecker checker, string framework)
    {
        checker.Processing($"Loading Nuget references of {Name}");

        var path = Path.GetFullPath($@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.nuget\packages\{Name}\{Version}");
        var nuspec = Path.Combine(path, $"{Name}.nuspec");
        if (!File.Exists(nuspec))
        {
            IsPresent = false;
            return;
        }

        var xml = new XmlDocument();
        xml.Load(nuspec);
        
        var references = xml
            .SelectNodes("//*[local-name()='package']/*[local-name()='metadata']/*[local-name()='dependencies']/*[local-name()='group']")
            ?.GetEnumerator();

        var depth = checker.Settings["Check"].Get("Depth", 2);
        var ignore = checker.Settings["Check"]
            .Get("Ignore", string.Empty)
            ?.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        while (references != null && references.MoveNext())
        {
            if (references.Current is not XmlNode reference) continue;

            var targetFramework = reference.Attributes?["targetFramework"]?.Value;
            if (targetFramework == null) continue;
            if (!targetFramework.StartsWith(".NETStandard", StringComparison.InvariantCultureIgnoreCase) && targetFramework != framework) continue;

            var dependencies = reference.SelectNodes("*[local-name()='dependency']")?.GetEnumerator();
            while (dependencies != null && dependencies.MoveNext())
            {
                if (dependencies.Current is not XmlNode dependency) continue;

                var name = dependency.Attributes?["id"]?.Value;
                var version = dependency.Attributes?["version"]?.Value;
                if (name == null || version == null) continue;
                
                if(name.StartsWith("System.", StringComparison.InvariantCultureIgnoreCase)) continue;
                if(name.StartsWith("Microsoft.NETCore.", StringComparison.InvariantCultureIgnoreCase)) continue;

                if (_depth > depth)
                {
                    checker.Processing($"Ignore Nuget {name} - max depth");
                    continue;
                }
                if (ignore.Any(i => name.StartsWith(i)))
                {
                    checker.Processing($"Ignore Nuget {name} - config");
                    continue;
                }
                
                var nugetRef = checker.NugetPackages.FirstOrDefault(nu => nu.Name == name && nu.Version == version);
                if (nugetRef == null)
                {
                    nugetRef = new NugetPackage(name, version, _depth + 1) { RefFrom = this };
                    nugetRef.LoadReferences(checker, framework);
                    checker.NugetPackages.Add(nugetRef);
                }
                References.Add(nugetRef);
            }
        }

    }
    
    
}