using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using IctBaden.Presentation;
using Microsoft.Win32;

namespace RefCheck
{
  public class MainViewModel : ActiveViewModel
  {
    public List<Reference> References { get; set; }
    public bool IsChecking { get; set; }
    [DependsOn("IsChecking")]
    public Visibility ShowChecking { get { return IsChecking ? Visibility.Visible : Visibility.Hidden; } }

    private Solution solution;
    private ReferenceChecker checker;
    private BackgroundWorker worker;

    public MainViewModel()
    {
      solution = Solution.Load("Open solution file...");
      SetModel("Solution", solution);
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

      this["IsChecking"] = true;

      worker = new BackgroundWorker();
      worker.DoWork += (sender, args) =>
      {
        this["References"] = new List<Reference>();

        solution = Solution.Load(dlg.FileName);
        SetModel("Solution", solution);

        checker = new ReferenceChecker(solution);
        checker.Check();
      };
      worker.RunWorkerCompleted += (sender, args) =>
      {
        UpdateReferences();
        this["IsChecking"] = false;
      };
      worker.RunWorkerAsync();
    }

    private void UpdateReferences()
    {
      References = solution.Projects.SelectMany(p => p.References).AsEnumerable()
        //.Distinct(new Reference())
        .OrderBy(r => r.ToString())
        .ToList();

      NotifyPropertyChanged("References");
    }

    [ActionMethod]
    public void SaveList()
    {
      if (checker == null)
        return;

      checker.Save();
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