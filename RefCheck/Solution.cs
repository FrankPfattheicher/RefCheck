using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IctBaden.Framework.IniFile;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace RefCheck
{
    public class Solution
    {
        public string FileName { get; private set; }
        public string Name { get; private set; }
        public string FormatVersion { get; private set; } = string.Empty;

        public Profile RefSettings { get; private set; }
        
        public List<Project> Projects { get; private set; }

        public bool IsLoaded => Projects.Count > 0;

        public string DisplayText => IsLoaded ? $"{FileName} ({Projects.Count} Projects)" : FileName;

        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public Solution(string fileName)
        {
            FileName = fileName;
            Name = Path.GetFileNameWithoutExtension(fileName);
            Projects = new List<Project>();
            Errors = new List<string>();
            Warnings = new List<string>();
            RefSettings = new Profile(Path.ChangeExtension(fileName, "references"));
        }

        public bool Load(ReferenceChecker checker)
        {
            // Microsoft Visual Studio Solution File, Format Version 12.00
            // # Visual Studio Version 16
            if (!File.Exists(FileName))
            {
                return false;
            }

            var lines = File.ReadAllLines(FileName);

            var format = lines.FirstOrDefault(line => line.StartsWith("Microsoft Visual Studio Solution File"));
            if (format != null)
            {
                FormatVersion = format.Split(',')[1].Trim();
            }

            var path = Path.GetDirectoryName(FileName) ?? ".";

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
                if (project.Load(checker))
                {
                    Projects.Add(project);
                }
            }
            
            return true;
        }

    }
}