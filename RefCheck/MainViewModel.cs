using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using IctBaden.Presentation;
using IctBaden.Presentation.Dialogs;
using IctBaden.Presentation.Menus;
using Microsoft.Win32;

namespace RefCheck
{
    public class MainViewModel : ActiveViewModel
    {
        public List<Reference> References { get; private set; }
        public bool IsChecking { get; private set; }

        public Visibility ShowChecking => IsChecking ? Visibility.Visible : Visibility.Hidden;
        public Visibility SolutionReady => IsInDesignMode || (solution.IsLoaded && !IsChecking) ? Visibility.Visible : Visibility.Hidden;

        public string CheckResult { get; private set; }
        [DependsOn(nameof(CheckResult))]
        public Visibility ShowResult => string.IsNullOrEmpty(CheckResult) ? Visibility.Collapsed : Visibility.Visible;

        private Solution solution;
        private ReferenceChecker checker;
        private BackgroundWorker worker;

        public MainViewModel()
        {
            solution = Solution.Load("Open solution file...");
            SetModel("Solution", solution);
        }

        public override void OnViewLoaded()
        {
            SystemMenu.AppendEntries(new List<SystemMenuEntry>
            {
                new SystemMenuEntry("-", null),
                new SystemMenuEntry("About RefCheck...", AboutDialog.ShowAboutDialog)
            });
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
            CheckResult = null;
            NotifyPropertiesChanged(new[] { nameof(ShowChecking), nameof(SolutionReady), nameof(CheckResult) });

            worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                References = new List<Reference>();

                solution = Solution.Load(dlg.FileName);
                SetModel("Solution", solution);

                checker = new ReferenceChecker(solution);
                checker.Check();
            };
            worker.RunWorkerCompleted += (sender, args) =>
            {
                UpdateReferences();
                if (solution.Warnings == 1)
                {
                    CheckResult = "1 Warning";
                }
                else if (solution.Warnings > 1)
                {
                    CheckResult = $"{solution.Warnings} Warnings";
                }
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