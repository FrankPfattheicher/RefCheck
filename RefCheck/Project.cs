using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using IctBaden.Framework.FileSystem;

namespace RefCheck
{
    public class Project
    {
        public string SolutionName { get; private set; }
        public string Name { get; private set; }
        public string RelativeName => FileSystemNaming.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(SolutionName)), Name);

        public List<Reference> References;

        public override string ToString()
        {
            return Name;
        }

        private Project()
        {
            References = new List<Reference>();
        }

        public static Project Load(string solutionFileName, string projectFileName)
        {
            var project = new Project { SolutionName = solutionFileName, Name = projectFileName };

            if (!File.Exists(projectFileName))
                return project;

            var xml = new XmlDocument();
            xml.Load(projectFileName);

            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var path = Path.GetDirectoryName(projectFileName) ?? ".";

            // ReSharper disable once PossibleNullReferenceException
            var references = xml
              .SelectNodes("//*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='Reference']")
              .GetEnumerator();

            while ((references != null) && references.MoveNext())
            {
                var reference = (XmlNode)references.Current;
                // ReSharper disable once PossibleNullReferenceException
                var include = reference.Attributes["Include"].Value.Split(',');

                var projRef = new Reference { Project = projectName, Name = include[0].Trim() };
                if (include.Length > 1)
                {
                    var options = include.Select(str => str.Split('='))
                      .Where(pair => pair.Length == 2)
                      .ToDictionary(pair => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pair[0].Trim()), pair => pair[1].Trim());

                    foreach (var option in options)
                    {
                        var pi = projRef.GetType().GetProperty(option.Key);
                        pi?.SetValue(projRef, option.Value);
                    }
                }

                var hintPath = reference.SelectSingleNode("*[local-name()='HintPath']");
                if (hintPath != null)
                {
                    projRef.SourcePath = Path.GetFullPath(Path.Combine(path, hintPath.InnerText));
                    projRef.IsPresent = File.Exists(projRef.SourcePath);
                    if (projRef.IsPresent)
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(projRef.SourcePath);
                        projRef.SourceVersion = fvi.ProductVersion
                            .Replace(fvi.ProductPrivatePart.ToString(), "")
                            .Trim('.');
                    }
                }

                project.References.Add(projRef);
            }


            return project;
        }
    }
}