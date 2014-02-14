using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace RefCheck
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    protected override void OnStartup(StartupEventArgs e)
    {
      if (e.Args.Length > 0)
      {
        // Command line given, display console
        if (!AttachConsole(-1))
        { // Attach to an parent process console
          AllocConsole(); // Alloc a new console
        }

        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;;
        var color = Console.ForegroundColor;
        ConsoleMain(e.Args);
        Console.ForegroundColor = color;
        return;
      }

      base.OnStartup(e);
    }

    private static void ConsoleMain(string[] args)
    {
      Console.WriteLine("RefCheck");

      var fileName = args[0];
      if (!File.Exists(fileName))
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Solution file not found: " + fileName);
        Current.Shutdown(2);
        return;
      }

      Console.WriteLine("Checking references for solution: " + fileName);

      var solution = Solution.Load(fileName);
      var checker = new ReferenceChecker(solution);
      foreach (var project in solution.Projects)
      {
        Console.WriteLine("Checking: " + project.Name);
        checker.Check(project);
      }

      var references = solution.Projects.SelectMany(p => p.References).AsEnumerable().ToList();
      Console.WriteLine("References found: " + references.Count);

      var blacklisted = references.Count(r => r.IsBlacklisted);
      if (blacklisted > 0)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        references.Where(r => r.IsBlacklisted).Count(r => { Console.WriteLine("Blacklisted: " + r); return true; });
        Console.WriteLine("Blacklisted references: " + blacklisted);
        Current.Shutdown(2);
        return;
      }

      var notwhitelisted = references.Count(r => !r.IsWhitelisted);
      if (notwhitelisted > 0)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;
        references.Where(r => r.IsBlacklisted).Count(r => { Console.WriteLine("Not whitelisted: " + r); return true; });
        Console.WriteLine("Not whitelisted references: " + notwhitelisted);
        Current.Shutdown(1);
        return;
      }

      Current.Shutdown(0);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int pid);
  }
}
