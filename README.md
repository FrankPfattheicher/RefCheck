RefCheck
========
A checker for VisualStudio solution project references.

Working on large VisualStudio solutions with lots of projects can lead to a mess of external references.
This is due the references are added in project scope and there is no solution wide synchronization.
The result is an error free build but unexpected results at runtime.

RefCheck is implemented as an dual-mode application. 
Executing without commandline parameters starts an UI that allows an interactive use.
Running with a solution filename as parameter is used within batch builds as commandline utility.

