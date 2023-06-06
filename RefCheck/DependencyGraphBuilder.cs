using System.Collections.Generic;
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

    public DependencyGraphBuilder(Solution solution, Profile refSettings, string fileName, bool includeSystemPackages)
    {
        _solution = solution;
        _refSettings = refSettings;
        _includeSystemPackages = includeSystemPackages;

        var fileStream = File.Create(fileName);
        _graph = new StreamWriter(fileStream);
    }

    public void BuildNugetGraph()
    {
        var name = Path.GetFileNameWithoutExtension(_solution.Name);
        _graph.WriteLine("@startuml");
        _graph.WriteLine($"title Nuget Packages of Solution {name}");

        foreach (var project in _solution.Projects)
        {
            _graph.WriteLine($"rectangle \"{project.ShortName}\" #8080FF");
        }

        var references = _solution.Projects
            .SelectMany(p => p.ProjectReferences)
            .Where(r => r.RefFrom != null)
            .Select(r => $"\"{r.RefFrom!.ShortName}\" -- \"{r.ShortName}\"");

        foreach (var line in references.Distinct())
        {
            _graph.WriteLine(line);    
        }
        
        
        var nugetReferences = _solution.Projects
            .SelectMany(p => p.NugetReferences)
            .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
            .DistinctBy(nu => nu.RefId)
            .ToList();
        foreach (var nugetReference in nugetReferences)
        {
            var groupName = nugetReference.Name.Split('.').First();
            var color = _refSettings["Color"].Get<string>(groupName) ?? nugetReference.Color;
            _graph.WriteLine($"component \"{nugetReference.Name}\" as {nugetReference.RefId} {color}");

            var projects = _solution.Projects
                .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
            foreach (var project in projects)
            {
                if (project.IsImplicitNugetReference(nugetReference))
                {
                    _graph.WriteLine($"\"{project.ShortName}\" =[#red]= {nugetReference.RefId} : redundant\\nreference");
                }
                else
                {
                    _graph.WriteLine($"\"{project.ShortName}\" -- {nugetReference.RefId}");
                }
            }
        }
        
        _graph.WriteLine("@enduml");
        _graph.Flush();
        _graph.Close();
    }
}