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


    public void BuildPlantUmlGraph()
    {
        _graph.WriteLine("@startuml");
        var graphLines = new List<string>();

        // ====== solutions ======
        foreach (var solution in _checker.Solutions)
        {
            graphLines.Add($"rectangle \"Solution\\n{solution.Name}\" as {solution.RefId} #FF8080");
        }

        // ====== projects ======
        var projects = _checker.Solutions.SelectMany(s => s.Projects).ToList();
        foreach (var project in projects)
        {
            graphLines.Add($"rectangle \"Projekt\\n{project.ShortName}\" as {project.RefId} #8080FF");
            graphLines.Add($"{project.Solution.RefId} -- {project.RefId}");
        }

        var projectReferences = projects.SelectMany(p => p.ProjectReferences)
            .DistinctBy(p => (p.RefFrom?.RefId ?? "") + p.RefId)
            .ToArray();

        foreach (var project in projectReferences)
        {
            if(project.RefFrom == null) continue;
            graphLines.Add($"{project.RefFrom.RefId} -- {project.RefId}");
        }

        // ====== project nuget references ======
        var nugetReferences = _checker.NugetPackages
            .DistinctBy(nu => (nu.RefFrom?.RefId ?? "") + nu.RefId)
            .ToArray();

        var addedReferences = new List<string>();

        foreach (var solution in _checker.Solutions)
        {
            var refSettings = solution.RefSettings;

            foreach (var nugetReference in nugetReferences)
            {
                var groupName = nugetReference.Name.Split('.').First();
                var color = refSettings["Color"].Get<string>(groupName) ?? nugetReference.Color;

                var isMixedNugetVersionReference = nugetReferences
                    .Any(nu => nu.Name == nugetReference.Name && nu.Version != nugetReference.Version);

                if (isMixedNugetVersionReference)
                {
                    color += ";line:red;line.bold";
                }

                graphLines.Add(
                    $"component \"{nugetReference.Name}\\n{nugetReference.Version}\" as {nugetReference.RefId} {color}");

                var solutionProjects = solution.Projects
                    .Where(p => p.NugetReferences.Any(n => n.RefId == nugetReference.RefId));
                foreach (var project in solutionProjects)
                {
                    var newRef = $"{project.RefId} -- {nugetReference.RefId}";
                    if (addedReferences.All(r => r != newRef))
                    {
                        graphLines.Add(project.IsImplicitNugetReference(nugetReference)
                            ? $"{project.RefId} =[#red]= {nugetReference.RefId} : redundant\\nreference"
                            : $"{project.RefId} -- {nugetReference.RefId}");
                        addedReferences.Add(newRef);
                    }
                }
            }

            // ====== nuget references to other nuget packages ======
            var innerReferences = nugetReferences
                .SelectMany(nu => nu.References)
                .Where(nu => _includeSystemPackages || !nu.IsSystemPackage)
                .DistinctBy(nu => (nu.RefFrom?.RefId ?? "") + nu.RefId)
                .ToList();

            foreach (var innerReference in innerReferences)
            {
                if (innerReference.RefFrom == null) continue;

                var newRef = $"{innerReference.RefFrom.RefId} -- {innerReference.RefId}";
                if (addedReferences.All(r => r != newRef))
                {
                    graphLines.Add($"\"{innerReference.RefFrom.RefId}\" -- {innerReference.RefId}");
                    addedReferences.Add(newRef);
                }
            }
        }

        foreach (var line in graphLines)
        {
            _graph.WriteLine(line);
        }

        _graph.WriteLine("@enduml");
        _graph.Flush();
        _graph.Close();
    }
}