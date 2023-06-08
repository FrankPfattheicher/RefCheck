using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RefCheck;

public class DependencyGraphBuilder
{
    private readonly ReferenceChecker _checker;
    private readonly bool _includeSystemPackages;

    private readonly StreamWriter _graph;

    public DependencyGraphBuilder(ReferenceChecker checker, string fileName, bool includeSystemPackages)
    {
        _checker = checker;
        _includeSystemPackages = includeSystemPackages;

        var fileStream = File.Create(fileName);
        _graph = new StreamWriter(fileStream);
    }

    private bool IsMixedNugetVersionReference(List<NugetPackage> nugetReferences, NugetPackage nugetReference) =>
        nugetReferences.Any(nu => nu.Name == nugetReference.Name && nu.Version != nugetReference.Version);

    
    public void BuildPlantUmlGraph()
    {
        _graph.WriteLine("@startuml");
        var graphLines = new List<string>();

        // ====== solutions ======
        foreach (var solution in _checker.Solutions)
        {
            graphLines.Add($"rectangle \"{solution.Name}\" as {solution.RefId} #FF8080");
        }
        
        // ====== projects ======
        var projects = _checker.Solutions.SelectMany(s => s.Projects).ToList();
        foreach (var project in projects)
        {
            graphLines.Add($"rectangle \"{project.ShortName}\" as {project.RefId} #8080FF");
            graphLines.Add($"{project.Solution.RefId} -- {project.RefId}");
        }
        foreach (var project in projects)
        {
            foreach (var projectReference in project.ProjectReferences)
            {
                graphLines.Add($"{project.RefId} -- {projectReference.RefId}");
            }
        }
        
        // ====== project nuget references ======
        var nugetReferences = _checker.NugetPackages;
        foreach (var solution in _checker.Solutions)
        {
            var refSettings = solution.RefSettings;
            
            foreach (var nugetReference in nugetReferences)
            {
                var groupName = nugetReference.Name.Split('.').First();
                var color = refSettings["Color"].Get<string>(groupName) ?? nugetReference.Color;

                if (IsMixedNugetVersionReference(nugetReferences, nugetReference))
                {
                    color += ";line:red;line.bold";
                }
            
                graphLines.Add($"component \"{nugetReference.Name}\\n{nugetReference.Version}\" as {nugetReference.RefId} {color}");

                var solutionProjects = solution.Projects
                    .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
                foreach (var project in solutionProjects)
                {
                    graphLines.Add(project.IsImplicitNugetReference(nugetReference)
                        ? $"{project.RefId} =[#red]= {nugetReference.RefId} : redundant\\nreference"
                        : $"{project.RefId} -- {nugetReference.RefId}");
                }
            }
        
            // ====== nuget references to other nuget packages ======
            var innerReferences = nugetReferences
                .SelectMany(nu => nu.References)
                .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
                .DistinctBy(nu => nu.RefId)
                .ToList();
            foreach (var innerReference in innerReferences)
            {
                if(innerReference.RefFrom == null) continue;
            
                var groupName = innerReference.Name.Split('.').First();
                var color = refSettings["Color"].Get<string>(groupName) ?? innerReference.Color;
                graphLines.Add($"component \"{innerReference.Name}\\n{innerReference.Version}\" as {innerReference.RefId} {color}");
            
                graphLines.Add($"\"{innerReference.RefFrom.RefId}\" -- {innerReference.RefId}");
            }
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