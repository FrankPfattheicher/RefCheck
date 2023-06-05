using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using IctBaden.Presentation;
using IctBaden.Presentation.Menus;
using Microsoft.Win32;

namespace RefCheck
{
    public class MainViewModel : ActiveViewModel
    {
        public List<Reference> References { get; private set; }
        public bool IsChecking { get; private set; }

        public Visibility ShowChecking => IsInDesignMode || IsChecking ? Visibility.Visible : Visibility.Hidden;
        public Visibility SolutionReady => IsInDesignMode || (solution.IsLoaded && !IsChecking) ? Visibility.Visible : Visibility.Hidden;


        [DependsOn(nameof(CheckProgress))]
        public string CheckSolution { get; private set; }
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string CheckProgress { get; private set; }

        public string CheckResult => checker.CheckResult;

        [DependsOn(nameof(CheckResult))]
        public Visibility ShowResult => string.IsNullOrEmpty(CheckResult) ? Visibility.Collapsed : Visibility.Visible;

        private Solution solution;
        private ReferenceChecker checker;
        private BackgroundWorker worker;

        public MainViewModel()
        {
            BindingPriority = DispatcherPriority.Send;
            solution = new Solution("Click here to open solution file...");
            checker = new ReferenceChecker(solution);
            SetModel("Solution", solution);
        }

        public override void OnViewLoaded()
        {
            SystemMenu.AppendDefaultAboutMenuEntry();
        }

        [ActionMethod]
        public void OpenProject()
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = true,
                DefaultExt = ".sln",
                Filter = "Solution files|*.sln"
            };

            if (dlg.ShowDialog() != true)
                return;

            IsChecking = true;
            NotifyPropertiesChanged(new[] { nameof(ShowChecking), nameof(SolutionReady) });

            worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                References = new List<Reference>();

                solution = new Solution();
                solution.Load(dlg.FileName);
                CheckSolution = solution.Name;
                SetModel("Solution", solution);

                checker = new ReferenceChecker(solution);
                checker.Processing += projectName =>
                {
                    this[nameof(CheckProgress)] = projectName;
                    Thread.Sleep(100);
                };
                checker.Check();
            };
            worker.RunWorkerCompleted += (sender, args) =>
            {
                UpdateReferences();
                IsChecking = false;
                NotifyPropertiesChanged(new[] { nameof(ShowChecking), nameof(SolutionReady), nameof(References), nameof(CheckResult) });
            };
            worker.RunWorkerAsync();
        }

        private void UpdateReferences()
        {
            References = solution.Projects
                .SelectMany(p => p.References).AsEnumerable()
                .OrderBy(r => r.ToString())
                .ToList();

            NotifyPropertyChanged("References");
        }

        [ActionMethod]
        public void SaveList()
        {
            checker?.Save();
        }

        [ActionMethod]
        public void WhitelistPresent()
        {
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            solution.Projects.SelectMany(p => p.References).AsEnumerable()
              .Where(r => r.IsPresent)
              .All(r => r.IsWhitelisted = true);

            UpdateReferences();
        }

    }
}