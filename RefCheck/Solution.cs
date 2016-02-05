using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RefCheck
{
    public class Solution
    {
        public string Name { get; private set; }
        public string FormatVersion { get; private set; }

        public List<Project> Projects { get; private set; }

        public bool IsLoaded => Projects.Count > 0;

        public string DisplayText => IsLoaded ? $"{Name} ({Projects.Count} Projects)" : Name;

        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public Solution()
        {
            Projects = new List<Project>();
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public static Solution Load(string fileName)
        {
            // Microsoft Visual Studio Solution File, Format Version 11.00
            // # Visual Studio 2010
            // Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "CoreData", "CoreData\CoreData.csproj", "{449FBB08-D1A5-4166-8AA0-E63DCC820148}"

            var solution = new Solution { Name = fileName };

            if (!File.Exists(fileName))
            {
                return solution;
            }

            var lines = File.ReadAllLines(fileName);

            var format = lines.FirstOrDefault(line => line.StartsWith("Microsoft Visual Studio Solution File"));
            if (format != null)
            {
                solution.FormatVersion = format.Split(',')[1].Trim();
            }

            var projectFiles = lines.Where(line => line.StartsWith("Project")).Select(line => line.Split(',')[1].Trim());

            var path = Path.GetDirectoryName(fileName) ?? ".";
            projectFiles = projectFiles.Select(name => Path.GetFullPath(Path.Combine(path, name.Substring(1, name.Length - 2))));

            solution.Projects = projectFiles.Where(File.Exists)
                .Select(pn => Project.Load(solution.Name, pn))
                .ToList();
            
            return solution;
        }

    }
}