using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml;

namespace RefCheck;

public class NugetPackage
{
    public string Name { get; }
    public string Version { get; }

    private bool _isWhitelisted;
    private bool _isBlacklisted;
    public bool IsPresent { get; set; }
    [Browsable(false)]
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

    [Browsable(false)]
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

    public NugetPackage? RefFrom { get; set; }


    public NugetPackage(string name, string version)
    {
        Name = name;
        Version = version;
    }

    public void LoadReferences(string framework)
    {
        Console.WriteLine($"Loading references of {Name}");

        var path = Path.GetFullPath($@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.nuget\packages\{Name}\{Version}");
        var nuspec = Path.Combine(path, $"{Name}.nuspec");
        if (!File.Exists(nuspec)) return;

        var xml = new XmlDocument();
        xml.Load(nuspec);
        
        var references = xml
            .SelectNodes("//*[local-name()='package']/*[local-name()='metadata']/*[local-name()='dependencies']/*[local-name()='group']")
            ?.GetEnumerator();

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
            
                var nugetRef = new NugetPackage(name, version)
                {
                    RefFrom = this
                };
                nugetRef.LoadReferences(framework);
                References.Add(nugetRef);
            }
        }

    }
    
    
}