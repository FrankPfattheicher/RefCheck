using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace RefCheck
{
  public class Project
  {
    public string Name { get; private set; }

    public List<Reference> References;

    public override string ToString()
    {
      return Name;
    }

    public Project()
    {
      References = new List<Reference>();
    }

    public static Project Load(string fileName)
    {
      var project = new Project { Name = fileName };

      if (!File.Exists(fileName))
        return project;

      var xml = new XmlDocument();
      xml.Load(fileName);

      var projectName = Path.GetFileNameWithoutExtension(fileName);
      var path = Path.GetDirectoryName(fileName);

      // ReSharper disable once PossibleNullReferenceException
      var references =
        xml
        .SelectNodes("//*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='Reference']")
        .GetEnumerator();
      while ((references != null) && references.MoveNext())
      {
        var reference = (XmlNode)references.Current;
        // ReSharper disable once PossibleNullReferenceException
        var include = reference.Attributes["Include"].Value.Split(new[] { ',' });

        var projRef = new Reference { Project = projectName, Name = include[0].Trim() };
        if (include.Length > 1)
        {
          var options = include.Select(str => str.Split(new[] {'='}))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pair[0].Trim()), pair => pair[1].Trim());

          foreach (var option in options)
          {
            var pi = projRef.GetType().GetProperty(option.Key);
            if(pi == null)
              continue;
            pi.SetValue(projRef, option.Value);
          }
        }

        var hintPath = reference.SelectSingleNode("*[local-name()='HintPath']");
        if (hintPath != null)
        {
          projRef.SourcePath = Path.GetFullPath(Path.Combine(path, hintPath.InnerText));
        }

        project.References.Add(projRef);
      }


      return project;
    }
  }
}