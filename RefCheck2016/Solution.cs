using System.Collections.Generic;
using System.IO;
using System.Linq;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

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

        public Solution(string name = null)
        {
            Name = name;
            Projects = new List<Project>();
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public bool Load(string fileName)
        {
            // Microsoft Visual Studio Solution File, Format Version 11.00
            // # Visual Studio 2010
            // Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "CoreData", "CoreData\CoreData.csproj", "{449FBB08-D1A5-4166-8AA0-E63DCC820148}"

            Name = fileName;

            if (!File.Exists(fileName))
            {
                return false;
            }

            var lines = File.ReadAllLines(fileName);

            var format = lines.FirstOrDefault(line => line.StartsWith("Microsoft Visual Studio Solution File"));
            if (format != null)
            {
                FormatVersion = format.Split(',')[1].Trim();
            }

            var path = Path.GetDirectoryName(fileName) ?? ".";

            var projectFiles = lines
                .Where(line => line.StartsWith("Project"))
                .Select(line => line.Split(',')[1].Trim());
            projectFiles = projectFiles
                .Where(name => name != "\"Solution Items\"")
                .Select(name => Path.GetFullPath(Path.Combine(path, name.Substring(1, name.Length - 2))));

            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    Errors.Add("Project file does not exist: " + projectFile);
                    continue;
                }
                Projects.Add(Project.Load(Name, projectFile));
            }
            
            return true;
        }

    }
}