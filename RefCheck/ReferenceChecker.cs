using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IctBaden.Framework.IniFile;
using Microsoft.Win32;

namespace RefCheck
{
  public class ReferenceChecker
  {
    private readonly Solution solution;
    private readonly Profile refList;
    private readonly Dictionary<string,string> frameworkAssemblies;

    public ReferenceChecker(Solution solution)
    {
      this.solution = solution;
      refList = new Profile(Path.ChangeExtension(solution.Name, "references"));

      var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NETFramework", false);
      if (regkey == null)
        return;

      var installRoot = regkey.GetValue("InstallRoot").ToString();
      string frameworkDir = Directory.EnumerateDirectories(installRoot, "v?.*").OrderByDescending(Path.GetFileName).First();

      var visualStudioToolsDir = Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .Where(kv => kv.Key.ToString().StartsWith("VS") && kv.Key.ToString().Contains("COMNTOOLS"))
        .OrderByDescending(kv => kv.Key)
        .Select(kv => kv.Value)
        .First()
        .ToString();
      string visualStudioCommonDir = Path.GetDirectoryName(Path.GetDirectoryName(visualStudioToolsDir));

      frameworkAssemblies = new Dictionary<string, string>();
      AddFrameworkDlls(frameworkDir);
      AddFrameworkDlls(visualStudioCommonDir);
    }

    private void AddFrameworkDlls(string path)
    {
      var frameworkDlls = Directory.EnumerateFileSystemEntries(path, "*.dll", SearchOption.AllDirectories);
      foreach (var dll in frameworkDlls)
      {
        var name = Path.GetFileNameWithoutExtension(dll).ToLower();
        if (!frameworkAssemblies.ContainsKey(name))
        {
          frameworkAssemblies.Add(name, dll);
        }
      }
    }

    public void Check()
    {
      foreach (var project in solution.Projects)
      {
        Check(project);
      }
    }

    public void Check(Project project)
    {
      foreach (var reference in project.References)
      {
        Check(reference);
      }
    }
    public void Check(Reference reference)
    {
      var listed = refList["List"].Get(reference.IniKey, 0);
      reference.IsWhitelisted = (listed == 1);
      reference.IsBlacklisted = (listed == 2);

      if (!string.IsNullOrEmpty(reference.SourcePath))
      {
        reference.IsPresent = File.Exists(reference.SourcePath);
        return;
      }

      if (!frameworkAssemblies.ContainsKey(reference.Name.ToLower()))
      {
        reference.IsPresent = false;
        return;
      }

      var path = frameworkAssemblies[reference.Name.ToLower()];
      reference.IsPresent = File.Exists(path);

      if (reference.IsPresent && string.IsNullOrEmpty(reference.SourcePath))
      {
        reference.SourcePath = path;
      }
    }

    public void Save()
    {
      foreach (var project in solution.Projects)
      {
        foreach (var reference in project.References)
        {
          var saved = refList["List"].Get(reference.IniKey, 0);
          var listed = reference.IsWhitelisted ? 1 : reference.IsBlacklisted ? 2 : 0;

          if (listed != saved)
          {
            refList["List"].Set(reference.IniKey, listed);
          }
        }
      }

      refList.Save();
    }

  }
}