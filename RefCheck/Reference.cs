using System.Collections.Generic;

namespace RefCheck
{
  public class Reference : IEqualityComparer<Reference>
  {
    public bool IsPresent { get; set; }
    public bool IsWhitelisted { get; set; }
    public bool IsBlacklisted { get; set; }

    public string Name { get; set; }
    public string Version { get; set; }
    public string Culture { get; set; }
    public string PublicKeyToken { get; set; }
    public string ProcessorArchitecture { get; set; }
    public string Project { get; set; }
    public string SourcePath { get; set; }

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

    public Reference()
    {
      Culture = "neutral";
    }

    public override string ToString()
    {
      var txt = Name;
      if (!string.IsNullOrEmpty(Version))
      {
        txt += " V" + Version;
      }
      return txt;
    }

    public bool Equals(Reference x, Reference y)
    {
      return (x.ToString() == y.ToString()) && (x.SourcePath == y.SourcePath);
    }

    public int GetHashCode(Reference obj)
    {
      return obj.ToString().GetHashCode();
    }
  }
}