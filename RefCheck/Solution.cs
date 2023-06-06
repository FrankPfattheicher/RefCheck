using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace RefCheck
{
    public class Solution
    {
        public string Name { get; private set; }
        public string FormatVersion { get; private set; } = string.Empty;

        public List<Project> Projects { get; private set; }

        public bool IsLoaded => Projects.Count > 0;

        public string DisplayText => IsLoaded ? $"{Name} ({Projects.Count} Projects)" : Name;

        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public Solution(string name)
        {
            Name = name;
            Projects = new List<Project>();
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public bool Load(string fileName)
        {
            // Microsoft Visual Studio Solution File, Format Version 12.00
            // # Visual Studio Version 16

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
                .Where(line => line.StartsWith("Project("))
                .Select(line => line.Split(',')[1].Trim())
                .Select(name => name.Substring(1, name.Length - 2));
            projectFiles = projectFiles
                .Where(name => name != "Solution Items")
                .Where(name => name.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase))
                .Select(name => Path.GetFullPath(Path.Combine(path, name)));

            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    Errors.Add("Project file does not exist: " + projectFile);
                    continue;
                }
                
                var project = new Project(this, projectFile);
                if (project.Load())
                {
                    Projects.Add(project);
                }
            }
            
            return true;
        }

    }
}