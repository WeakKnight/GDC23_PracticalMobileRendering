# Python Scripting Settings

You can find preferences for Python Scripting in the Project Settings under the
Edit menu.

![Python Scripting Settings](images/project-settings.png)

The top of the window is useful information when looking for help to explain
exactly which version of the various components you are using.

## Component Versions

Python Scripting bundles with it a copy of the CPython implementation for Python
along with a custom build of Python for .NET. For information purposes the
Settings panel verifies which version of each is in fact installed. They cannot
be changed.

## Site Packages

You can add site-packages that will be added to the `sys.path` to find
additional Python modules. Changes are only applied after you restart Unity.

![Python Scripting Site Packages Settings](images/project-settings-site-packages.png)

<a name="pipPackages"></a>

## Pip Packages

You can install Python packages using pip for your project. They will be installed
inside the `Library/PythonInstall/Lib/site-packages` folder of the project.

### Installing packages with a requirements file

You can store your required Python packages with their versions in a `requirements.txt`
file. To accomodate multiple Unity packages requiring different pip packages, this 
requirements file is made to be used with `pip-tools`' [`compile`](https://github.com/jazzband/pip-tools#example-usage-for-pip-compile) utility.
The same required package may appear more than once, as long both requirements 
allow the same version ranges. Example :`pyside2==5.15.1` and `pyside2>=5.15`.

If you want to share your Unity project with someone else or you are working in a
team, sharing the `requirements.txt` file will ensure everyone is using the same
versions of the same packages. Simply opening a project with a `requirements.txt` file
will automatically install any missing packages, and uninstall unused ones.

Place the requirements file in `ProjectSettings/requirements.txt` in your Unity project
for the Python Scripting package to find it.

For details on creating a requirements file and its uses, please refer to the [pip documentation](https://pip.pypa.io/en/stable/user_guide/#requirements-files) and [`pip-tools`' documentation](https://pip-tools.readthedocs.io/en/stable/)

Unity will only apply the `requirements.txt` when you open a project.
If you change the `requirements.txt` while you have a project open (for example, if you update your project from revision control while Unity is running),
the Python packages will not update. To apply the new requirements you will need to restart Unity.

## Limitations

* You cannot change the internal Python version nor use a virtual environment manager like `venv` or `conda`.
* Paths are treated as verbatim strings. You will need to expand environment
  variables or (on macOS and linux) the `~` denoting the home directory.
