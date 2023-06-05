using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using IctBaden.Framework.AppUtils;

namespace RefCheck.Tests
{
    public static class TestSettings
    {
        public static string SolutionFileName
        {
            get
            {
                //var solutionPath = AssemblyInfo.Default.ExePath;
                //var testIndex = solutionPath.IndexOf("RefCheck.Tests", StringComparison.InvariantCulture);
                //solutionPath = solutionPath.Substring(0, testIndex);
                //return Path.Combine(solutionPath, @"TestSolution\TestSolution.sln");
                return @"C:\Users\Frank\Documents\ICT Baden\git\RefCheck\TestSolution\TestSolution.sln";
            }
        }
    }
}