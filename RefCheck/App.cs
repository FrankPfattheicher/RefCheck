using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace RefCheck
{
    public static class App
    {
        public enum ReturnCode
        {
            Ok = 0,
            Warning = 1,
            Error = 2,
            Fatal = 3
        }

        private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();

            var name = assemblyName.Name + ".lib." + args.Name.Split(',')[0] + ".dll";
            
            using (var stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    return null;

                using (var reader = new BinaryReader(stream))
                {
                    var data = reader.ReadBytes((int)stream.Length);
                    return Assembly.Load(data);
                }
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;

            if (args.Length > 0)
            {
                // Command line given, display console
                if (!AttachConsole(-1))
                { // Attach to an parent process console
                    AllocConsole(); // Alloc a new console
                }

                var color = Console.ForegroundColor;
                var exitCode = (int)ConsoleMain(args);
                Console.ForegroundColor = color;

                FreeConsole();
                SendKeys.SendWait("{ENTER}");
                Environment.Exit(exitCode);
            }

            WpfMain();
        }

        private static void WpfMain()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }

        private static ReturnCode ConsoleMain(string[] args)
        {
            Console.WriteLine(@"RefCheck");
            var defaultColor = Console.ForegroundColor;

            var fileName = args[0];
            if (!File.Exists(fileName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"Solution file not found: " + fileName);
                return ReturnCode.Fatal;
            }

            var solution = new Solution();
            solution.Load(fileName);
            Console.WriteLine(@"Solution: " + solution.Name);
            foreach (var error in solution.Errors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(@"Error: " + error);
            }

            Console.WriteLine($"Checking {solution.Projects.Count} projects.");

            var checker = new ReferenceChecker(solution);
            Console.WriteLine($"Checking {checker.References.Count} references.");

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

            Console.ForegroundColor = defaultColor;
            Console.WriteLine(@"Check done.");

            if (solution.Errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(checker.CheckResult);
                return ReturnCode.Error;
            }
            if (solution.Warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(checker.CheckResult);
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
