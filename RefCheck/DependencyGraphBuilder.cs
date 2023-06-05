using System.IO;
using System.Linq;

namespace RefCheck;

public class DependencyGraphBuilder
{
    private readonly Solution _solution;
    private readonly bool _includeSystemPackages;

    private readonly StreamWriter _graph;

    public DependencyGraphBuilder(Solution solution, string fileName, bool includeSystemPackages)
    {
        _solution = solution;
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

            foreach (var projectReference in project.ProjectReferences)
            {
                _graph.WriteLine($"\"{project.ShortName}\" -- \"{projectReference.ShortName}\"");
            }
        }
        
        var nugetReferences = _solution.Projects
            .SelectMany(p => p.NugetReferences)
            .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
            .DistinctBy(nu => nu.RefId)
            .ToList();
        foreach (var nugetReference in nugetReferences)
        {
            _graph.WriteLine($"component \"{nugetReference.Name}\" as {nugetReference.RefId} {nugetReference.Color}");

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