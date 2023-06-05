using System;
using System.Collections.Generic;
using System.ComponentModel;

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
    public string RefId => $"{Name.Replace("-", "_")}_{Version}";

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


    public NugetPackage(string name, string version)
    {
        Name = name;
        Version = version;
    }
}