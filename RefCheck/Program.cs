using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IctBaden.Framework.AppUtils;

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
        Console.WriteLine("RefCheck");
        var defaultColor = Console.ForegroundColor;
        var buildGraph = true;

        if (!args.Any())
        {
            //TODO: Start UI
        }

        var solutionFileNames = new List<string>();
        foreach (var arg in args)
        {
            if (!File.Exists(arg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"Solution file not found: " + arg);
                return (int)AppReturnCode.Fatal;
            }
            solutionFileNames.Add(arg);
        }

        var checker = new ReferenceChecker();

        checker.OnProcessing += projectName =>
        {
            Console.ForegroundColor = defaultColor;
            Console.WriteLine(projectName);
        };
        checker.OnError += error =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"Error: " + error);
        };
        checker.OnWarning += warning =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(@"Warning: " + warning);
        };

        checker.CheckSolutions(solutionFileNames);

        if (buildGraph)
        {
            var pumlName = "References.puml";
            Console.WriteLine($"Building dependency graph {pumlName}");
            var graphBuilder = new DependencyGraphBuilder(checker, pumlName, false);
            graphBuilder.BuildPlantUmlGraph();
        }
        
        
        Console.ForegroundColor = defaultColor;
        Console.WriteLine(@"Check done");

        if (checker.Errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(checker.CheckResult);
            return (int)AppReturnCode.Error;
        }

        if (checker.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(checker.CheckResult);
            return (int)AppReturnCode.Warning;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("No Errors, no warnings");
        return (int)AppReturnCode.Ok;
    }
}