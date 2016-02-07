RefCheck
=========
A checker for VisualStudio solution project references.

Working on large VisualStudio solutions with lots of projects can lead to a mess of external references.

Software projects that contain a solution with more than a single project and more than a single developer
tend to be infected with inconsistent nuget references.

This is due the references are added in project scope and there is no solution wide synchronization.
The result is an error free build, but unexpected results at runtime.

RefCheck is implemented as an dual-mode application. 
Executing without commandline parameters starts an UI that allows an interactive use.
Running with a solution filename as parameter is used within batch builds as commandline utility.

The sample project
-------------------
contains NO CODE but just some projects to show how the references mess up.

There is console application and three library projects.
The application references all three libraies.
Each of the libraries references the nuget packet Newtonsoft.Json.

Let's assume the libraries are set up one after anoter in ther entire project's lifetime.
During this time the revision of the Newtonsoft.Json library changed from 6.0.1 to 6.0.3.
The developer simply adds the nuget reference with the current version as he set up the library project.

Now the result is a solution wide difference in the effecive refernce to Newtonsoft.Json.

Depending on the build output folder the result may vary.
(1) Same output folder for all projects in solution => the reference of the last modified library is used.
or
(2) The first in the main application referenced library's reference is used.

The interactve view of RefCheck shows this like this.
![RefCheck interactive](https://github.com/FrankPfattheicher/RefCheck/blob/master/doc/RefCheck1.png)

As you see, all three libraries has a reference to Newtonsoft.Json 6.0.0.0
but looking in detail every library effectively includes a different version of it.
![RefCheck interactive](https://github.com/FrankPfattheicher/RefCheck/blob/master/doc/RefCheck2.png)

Using RefChack as a commandline utility ti shows the following result.
![RefCheck interactive](https://github.com/FrankPfattheicher/RefCheck/blob/master/doc/RefCheck3.png)

The warning is returned as ERRORLEVEL 1.

