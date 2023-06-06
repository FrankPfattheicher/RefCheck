using System;
using System.IO;
using System.Reflection;
using IctBaden.Framework.AppUtils;
using IctBaden.Framework.IniFile;

[assembly: AssemblyDescription("VisualStudio solution reference checking tool")]
[assembly:
    AssemblyContact(Address = "Seboldstraße 9", City = "76227 Karlsruhe", Mail = "support@ict-baden.de",
        Phone = "0172-7207196", Url = "https://www.ict-baden.de")]
[assembly: AssemblyCopyright("Copyright ©2014-2023 ICT Baden GmbH")]

namespace RefCheck;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine(@"RefCheck");
        var defaultColor = Console.ForegroundColor;
        var buildGraph = true;

        var fileName = args[0];
        if (!File.Exists(fileName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"Solution file not found: " + fileName);
            return (int)AppReturnCode.Fatal;
        }

        var solutionName = Path.GetFileNameWithoutExtension(fileName);
        var solution = new Solution(solutionName);
        solution.Load(fileName);
        Console.WriteLine(@"Solution: " + solution.Name);
        foreach (var error in solution.Errors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"Error: " + error);
        }

        Console.WriteLine($"Checking {solution.Projects.Count} projects");

        var refSettings = new Profile(Path.ChangeExtension(fileName, "references"));
        Console.WriteLine($"Using settings from {refSettings.FileName}");
        
        var checker = new ReferenceChecker(solution, refSettings);
        Console.WriteLine($"Checking {checker.Projects.Count} references");

        checker.Processing += projectName =>
        {
            Console.ForegroundColor = defaultColor;
            Console.WriteLine(projectName);
        };
        checker.Error += error =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"Error: " + error);
        };
        checker.Warning += warning =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(@"Warning: " + warning);
        };

        checker.Check();

        if (buildGraph)
        {
            var pumlName = Path.ChangeExtension( solution.Name, "puml");
            Console.WriteLine($"Building dependency graph {pumlName}");
            var graphBuilder = new DependencyGraphBuilder(solution, refSettings, pumlName, false);
            graphBuilder.BuildNugetGraph();
        }
        
        
        Console.ForegroundColor = defaultColor;
        Console.WriteLine(@"Check done");

        if (solution.Errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(checker.CheckResult);
            return (int)AppReturnCode.Error;
        }

        if (solution.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(checker.CheckResult);
            return (int)AppReturnCode.Warning;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"No Errors, no warnings");
        return (int)AppReturnCode.Ok;
    }
}