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
        private readonly Dictionary<string, string> frameworkAssemblies;
        private bool checkDone;

        public readonly List<Reference> References;

        public event Action<string> Processing;
        public event Action<string> Error;
        public event Action<string> Warning;

        public ReferenceChecker(Solution solution)
        {
            this.solution = solution;
            refList = new Profile(Path.ChangeExtension(solution.Name, "references"));

            var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NETFramework", false);
            if (regkey == null)
                return;

            var installRoot = regkey.GetValue("InstallRoot").ToString();
            var frameworkDir = Directory.EnumerateDirectories(installRoot, "v?.*").OrderByDescending(Path.GetFileName).First();

            var visualStudioToolsDir = Environment.GetEnvironmentVariables()
              .Cast<DictionaryEntry>()
              .Where(kv => kv.Key.ToString().StartsWith("VS") && kv.Key.ToString().Contains("COMNTOOLS"))
              .OrderByDescending(kv => kv.Key)
              .Select(kv => kv.Value)
              .First()
              .ToString();
            var visualStudioCommonDir = Path.GetDirectoryName(Path.GetDirectoryName(visualStudioToolsDir));

            frameworkAssemblies = new Dictionary<string, string>();
            AddFrameworkDlls(frameworkDir);
            AddFrameworkDlls(visualStudioCommonDir);

            References = solution.Projects
                                .SelectMany(p => p.References).AsEnumerable()
                                .OrderBy(r => r.ToString())
                                .ToList();
        }

        private void AddFrameworkDlls(string path)
        {
            var frameworkDlls = Directory.EnumerateFileSystemEntries(path, "*.dll", SearchOption.AllDirectories);
            foreach (var dll in frameworkDlls)
            {
                var name = Path.GetFileNameWithoutExtension(dll)?.ToLower() ?? "";
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

            if (References.Count > 0)
            {
                CheckForMixedReferences();
                CheckForBlacklistedReferences();
            }

            checkDone = true;
        }

        private void Check(Project project)
        {
            Processing?.Invoke(project.RelativeName);

            foreach (var reference in project.References)
            {
                Check(reference);
            }
        }

        private void Check(Reference reference)
        {
            reference.IsWhitelisted = refList["Whitelisted"].Get(reference.IniKey, false);
            reference.IsBlacklisted = refList["Blacklisted"].Get(reference.IniKey, false);

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

        private void CheckForMixedReferences()
        {
            Processing?.Invoke("Check for mixed references...");

            var refGroups = References.GroupBy(r => r.RefId)
                .Select(g => g.AsQueryable().ToList())
                .Where(rl => rl.Count > 1)
                .ToList();

            var warningRefs = refGroups.Select(g => g.GroupBy(r => r.SourceId).ToList())
                .Where(rg => rg.Count > 1)
                .ToList();

            foreach (var warningRef in warningRefs)
            {
                var wref = warningRef.First().AsEnumerable().First();
                var versions = string.Join(" <-> ",
                    warningRef.SelectMany(w => w.AsEnumerable())
                    .Select(r => r.SourceVersion).Distinct()
                    );

                var warning = $"{wref.Name} {wref.Version}, used: {versions}";
                solution.Warnings.Add(warning);
                Warning?.Invoke(warning);
            }

            var referenced = warningRefs
                .SelectMany(g => g.AsEnumerable())
                .Select(g => g.Key)
                .ToList();

            foreach (var reference in References)
            {
                reference.IsWarning = referenced.Contains(reference.SourceId);
            }
        }

        public void CheckForBlacklistedReferences()
        {
            Processing?.Invoke("Check for blacklisted references...");

            foreach (var reference in References.Where(r => !r.IsWhitelisted))
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

        public string CheckResult
        {
            get
            {
                if (!checkDone) return null;

                var result = string.Empty;
                switch (solution.Errors.Count)
                {
                    case 0:
                        result += "No errors";
                        break;
                    case 1:
                        result += "1 error";
                        break;
                    default:
                        result += $"{solution.Errors.Count} errors";
                        break;
                }
                result += ", ";
                switch (solution.Warnings.Count)
                {
                    case 0:
                        result += "no warnings";
                        break;
                    case 1:
                        result += "1 warning";
                        break;
                    default:
                        result += $"{solution.Warnings.Count} warnings";
                        break;
                }

                return result;
            }
        }

        public void Save()
        {
            foreach (var project in solution.Projects)
            {
                foreach (var reference in project.References)
                {
                    var isWhitelisted = refList["Whitelisted"].Get(reference.IniKey, false);
                    var isBlacklisted = refList["Blacklisted"].Get(reference.IniKey, false);

                    if (reference.IsWhitelisted != isWhitelisted)
                    {
                        refList["Whitelisted"].Set(reference.IniKey, reference.IsWhitelisted);
                    }
                    if (reference.IsBlacklisted != isBlacklisted)
                    {
                        refList["Blacklisted"].Set(reference.IniKey, reference.IsBlacklisted);
                    }
                }
            }

            refList.Save();
        }

    }
}
