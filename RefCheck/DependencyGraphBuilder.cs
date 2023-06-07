﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using IctBaden.Framework.IniFile;

namespace RefCheck;

public class DependencyGraphBuilder
{
    private readonly Solution _solution;
    private readonly Profile _refSettings;
    private readonly bool _includeSystemPackages;

    private readonly StreamWriter _graph;
    private readonly List<NugetPackage> _nugetReferences;

    public DependencyGraphBuilder(Solution solution, Profile refSettings, string fileName, bool includeSystemPackages)
    {
        _solution = solution;
        _refSettings = refSettings;
        _includeSystemPackages = includeSystemPackages;

        var fileStream = File.Create(fileName);
        _graph = new StreamWriter(fileStream);
        
        _nugetReferences = _solution.Projects
            .SelectMany(p => p.NugetReferences)
            .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
            .ToList();
    }

    private bool IsMixedNugetVersionReference(NugetPackage nugetReference) =>
        _nugetReferences.Any(nu => nu.Name == nugetReference.Name && nu.Version != nugetReference.Version);

    
    public void BuildPlantUmlGraph()
    {
        var name = Path.GetFileNameWithoutExtension(_solution.Name);
        _graph.WriteLine("@startuml");
        _graph.WriteLine($"title Nuget Packages of Solution {name}");

        var graphLines = new List<string>();
        
        // ====== projects ======
        foreach (var project in _solution.Projects)
        {
            graphLines.Add($"rectangle \"{project.ShortName}\" #8080FF");
        }
        foreach (var project in _solution.Projects)
        {
            foreach (var projectReference in project.ProjectReferences)
            {
                graphLines.Add($"\"{project.ShortName}\" -- \"{projectReference.ShortName}\"");
            }
        }
        
        // ====== project nuget references ======
        foreach (var nugetReference in _nugetReferences)
        {
            var groupName = nugetReference.Name.Split('.').First();
            var color = _refSettings["Color"].Get<string>(groupName) ?? nugetReference.Color;

            if (IsMixedNugetVersionReference(nugetReference))
            {
                color += ";line:red;line.bold";
            }
            
            graphLines.Add($"component \"{nugetReference.Name}\\n{nugetReference.Version}\" as {nugetReference.RefId} {color}");

            var projects = _solution.Projects
                .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
            foreach (var project in projects)
            {
                graphLines.Add(project.IsImplicitNugetReference(nugetReference)
                    ? $"\"{project.ShortName}\" =[#red]= {nugetReference.RefId} : redundant\\nreference"
                    : $"\"{project.ShortName}\" -- {nugetReference.RefId}");
            }
        }
        
        // ====== nuget references to other nuget packages ======
        var innerReferences = _nugetReferences
            .SelectMany(nu => nu.References)
            .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
            .DistinctBy(nu => nu.RefId)
            .ToList();
        foreach (var innerReference in innerReferences)
        {
            if(innerReference.RefFrom == null) continue;
            
            var groupName = innerReference.Name.Split('.').First();
            var color = _refSettings["Color"].Get<string>(groupName) ?? innerReference.Color;
            graphLines.Add($"component \"{innerReference.Name}\\n{innerReference.Version}\" as {innerReference.RefId} {color}");
            
            graphLines.Add($"\"{innerReference.RefFrom.RefId}\" -- {innerReference.RefId}");
        }
        
        foreach (var line in graphLines.Distinct())
        {
            _graph.WriteLine(line);    
        }
        _graph.WriteLine("@enduml");
        _graph.Flush();
        _graph.Close();
    }
}