using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace RefCheck
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public enum ReturnCode
        {
            Ok = 0,
            Warning = 1,
            Error = 2,
            Fatal = 3
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                // Command line given, display console
                if (!AttachConsole(-1))
                { // Attach to an parent process console
                    AllocConsole(); // Alloc a new console
                }

                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown; ;
                var color = Console.ForegroundColor;
                var exitCode = (int)ConsoleMain(e.Args);
                Console.ForegroundColor = color;

                FreeConsole();
                SendKeys.SendWait("{ENTER}");
                Environment.Exit(exitCode);
            }

            base.OnStartup(e);
        }

        private static ReturnCode ConsoleMain(string[] args)
        {
            Console.WriteLine(@"RefCheck");

            var fileName = args[0];
            if (!File.Exists(fileName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"Solution file not found: " + fileName);
                return ReturnCode.Fatal;
            }

            var solution = Solution.Load(fileName);
            Console.WriteLine(@"Solution: " + solution.DisplayText);

            var checker = new ReferenceChecker(solution);
            foreach (var project in solution.Projects)
            {
                Console.WriteLine(@"Checking: " + project.Name);
                checker.Check(project);
            }
            checker.CheckForWarnings();

            var references = solution.Projects.SelectMany(p => p.References).AsEnumerable().ToList();
            Console.WriteLine(@"References found: " + references.Count);

            var blacklisted = references.Where(r => r.IsBlacklisted).ToList();
            if (blacklisted.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                blacklisted.ForEach(r => { Console.WriteLine(@"Blacklisted: " + r); });
                Console.WriteLine(@"Blacklisted references: " + blacklisted.Count);
                return ReturnCode.Error;
            }

            //var notwhitelisted = references.Where(r => !r.IsWhitelisted).ToList();
            //if (notwhitelisted.Count > 0)
            //{
            //    Console.ForegroundColor = ConsoleColor.Yellow;
            //    references.ForEach(r => { Console.WriteLine(@"Not whitelisted: " + r); });
            //    Console.WriteLine(@"Not whitelisted references: " + notwhitelisted.Count);
            //    return ReturnCode.Warning;
            //}

            if (solution.Errors > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(solution.Errors + @" error" + (solution.Errors > 1 ? "s" : "") + @".");
                return ReturnCode.Error;
            }
            if (solution.Warnings > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(solution.Warnings + @" warning" + (solution.Warnings > 1 ? "s" : "") + @".");
                return ReturnCode.Warning;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(@"No Errors, no warnings.");
            return ReturnCode.Ok;
        }

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
    }
}
