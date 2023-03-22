# Changes in Python Scripting

RELEASE NOTES
## [7.0.0-pre.1] - 2022-08-30
REMOVED
* Removed the remnants of the Python Client API
* Removed pycoverage initialization/config

KNOWN ISSUES
* Some Pypi libraries are Intel only on Mac

CHANGES
* Added a C# API to launch an external process with the PATH set up to point to the Python distribution.
* Added a C# API to add pip packages
* Added a C# API to add to site packages
* Update minimum Unity version to 2020.3
* Updated the binaries package to 1.3.0-pre.2, which upgrades Python to version 3.10.6

## [6.0.0] - 2022-08-04
* Fixes multiple problems with C# objects lifetime and domain reloads.
* Updated the Python for .NET DLL to a custom patch, based on the official version 3.0.0-rc4. Commit hash: 7ffad42406e36cda9e4be7eb4ad533e45502a60d [of Unity's Pythonnet fork](https://github.com/Unity-Technologies/pythonnet/)

## [5.0.0-pre.5] - 2022-04-29
* Updated the binaries package to 1.2.0-pre.2, which upgrades Python to version 3.9.10
* Added Ubuntu 18.04, 20.04 support
* Added native macOS arm64 support
* Updated the Python for .NET DLL to 3.0.0-a2

REMOVED
* pip-tools is now packaged as part of the binary distribution

## [4.0.0-pre.1] - 2021-10-15
* updated installation instructions with separate macOS and Windows sections
* fixed tests failing due to new pip version
* fixed tests failing due to package lifecycle v2

## [4.0.0-exp.5] - 2021-03-26

This is the first public release of Python Scripting since the 2.0.1-preview.2 release.

It incorporates a large number of changes you can see below for intermediate versions released in limited alpha distribution. In summary from the last public version:

MAJOR CHANGES from 2.0.1-preview.2:
* Based on Python 3.7; scripts based on Python 2.7 will need to be ported.
* Users no longer need to install Python on their system.
* In-process Python is no longer reinitialized when the Unity domain reloads.
* Removed the Out-of-process API. The PySide example now runs in-process and is much simpler.
* Limited support for a virtual environment workflow via the `ProjectSettings/requirements.txt` file.
* Many bug fixes.

## [4.0.0-exp.4] - 2021-02-26

This is a pre-alpha version for internal testing.

FIXES
* Python for .NET DLL on Windows updated to 9d0bd9c2bbee00f09f9948437879b557b6202533 [of Unity's Pythonnet fork](https://github.com/Unity-Technologies/pythonnet/)
* Python for Windows updated to 3.7.9, to match the macOS version.

CHANGES
* Python no longer gets reloaded during a domain reload. This means:
    * You can use many more native-implemented modules in-process, e.g. PySide2, numpy.
    * To reinitialize Python (e.g. to reload all your modules) you need to restart Unity.
    * Updated the PySide example to show how to build a Qt-based user interface in the main process.

* Removed the out-of-process API.
    * The `unity_python.server` and `unity_python.client` modules no longer exist.
    * Spawning a subprocess in the environment is now via the `unity_python.common.spawn_process` module (previously via the server module).
    * Removed the eval/exec and REPL examples since they are no longer relevant.

* The pip requirements are now in `ProjectSettings/requirements.txt` instead of `Assets/pip.requirements` or a custom path.
    * Requirements can no longer be created/modified from Unity editor (text file must be edited manually).
    * Requirements are applied when first starting Unity. If they are modified during a Unity session, Unity must be restarted to apply them.
    * Removed UI related to pip requirements from Python Scripting settings in Edit > Project Settings...
    * Can no longer change path to requirements file.


## [4.0.0-exp.3] - 2020-12-07

This is a pre-alpha version for internal testing.

FIXES
* Fixed redirecting python print to the Python Console output.
* Fixes some cases undefined behaviour occurring after a domain reload when Python data structures store references to .NET classes and members and those members are removed from their classes. Some changes may still trigger undefined behaviour.
* No longer print log message when importing pip.requirements if everything up to date.
* Fixed typing in Python Console Window dirties scene.
* Fixed undo/redo in Python Console Window has no effect if text area in focus.

## [4.0.0-exp.2] - 2020-10-02

This is a pre-alpha version for internal testing.

NEW FEATURES:
* Reinstated support for macOS

## [4.0.0-exp.1] - 2020-09-22

This is a pre-alpha version for internal testing.

NEW FEATURES
* Update to the commit hash efabeba6e3edfd17ea9cd2e84b75b1268155f64b of (python.net|https://github.com/Unity-Technologies/pythonnet/tree/soft-shutdown-demo) which adds improved support for domain reload. This allows:
** Import Python modules with native implementation (e.g. PySide2, tensorflow) into Unity itself
** Python data structures keep their data through a domain reload

INCOMPATIBLE CHANGES
* Python data structures are not reinitialized during domain reload

KNOWN ISSUES
* Undefined behaviour occurs after a domain reload when Python data structures store references to .NET classes and members and those members are removed from their classes or the classes are removed from their assemblies
* Undefined behaviour occurs after a domain reload if Python accesses a .NET assembly and that assembly is removed from the project

## [3.2.0-preview.1] - 2020-09-01

NEW FEATURES
* Support for macOS

## [3.1.0-preview.1] - 2020-07-15

NEW FEATURES
* SpawnClient can now optionally hide the window that pops up on Windows.
* Python for .NET updated to version 2.5.1

FIXES
* Windows command consoles no longer pop up as often.
* A Warning will be emitted in the Console in the event there is a need to update the binaries package.
* Fixed a bug where Python's print was raising exceptions due to a null stdout.
* Fixed a bug where list comprehensions would raise NameError when referencing local or global variables.

## [3.0.0-preview.12] - 2020-06-11
REMOVED
* Support for macOS and Linux

NEW FEATURES
* Python upgraded to Python 3.7.6
* A Python install is no longer required to use this pacakge. A local python install is done for each project.
* Multiple instances of Unity using Python Scripting can now run at the same time.
* Added support for undo and redo in the Python Console.
* Added Pip packages support. A Pip requirements file is generated when the scene is saved. Packages are restored when the Library is regenerated or the file is modified.
* Added a button in the Python Settings window to spawn a system shell using the same environment as the spawned Python clients.
* Added a new logging Python module to log to Unity's console, the Editor log file, the Standard Output or to another file. It is also possible to log to Unity's console over RPyC for Python clients.
* Improved the error logging of raised exceptions when trying to reconnect a Python client

FIXES
* Fixed various issues with paths and spaces for extra site-packages and the out-of-process Python interpreter.
* Fixed an issue where the Python console lost its Standard Output after a domain reload.
* Fixed an issue in the Python console where the previously written code would be executed instead after loading a file.
* Fixed an issue with the installation logic where the binaries packages would cause an infinite loop if the package is embedded in the Packages folder.
* Fixed an issue where the `__name__` variable would not be `__main__` when executing code in the Python console.
* Fixed an issue where the `__name__` variable would not be `__main__` when using `PythonRunner.RunFile` and `PyhonRunner.RunString`.
* Fixed an issue in the Python settings windows where the File Browser would fail to open if the current Python interpreter path doesn't exist
* Fixed an issue where user-defined assemblies and assemblies added by the Package Manager would not be accessible to the `clr` python module.

KNOWN ISSUES
* The python console's output is limited to 10 000 characters. In case the output grows beyond that number, older characters are truncated.

## [2.0.1-preview.2] - 2020-02-13

This is a bugfix release for 2.0.0-preview.

FIXES
* Improved handling of a Python installation that can't find its home. Unity now displays an error rather than crashing.
* Fixed repeated registration of an OnUpdate callback.
* Prevented the Python top-level menu being added by default. Unpack the sample clients manually from the Package Manager instead.
* The tilde character (~) in Python path and site-packages is now interpreted as the user home directory on all platforms.
* In the Python Console, Ctrl-enter with no selection now executes the entire script (Cmd-enter on macOS).
* Fixed the year in 2.0.0-preview.6 release date below. It's already 2020 apparently.

## [2.0.0-preview.6] - 2020-01-08

This is the first public release of Python Scripting.

## [2.0.0-preview] - 2019-07-17

BREAKING CHANGE
* The out-of-process API has been entirely rewritten. The new API supports multiple clients, and asynchronous calls. Clients written to the previous API will need to be updated.

NEW FEATURES
* Python Console; find it in [menu]
* Python for .NET updated to version 2.4.0

## [1.3.2-preview] - 2019-06-10

FIXES
- Fixed Python initialization problem on OSX

RELEASE NOTES
## [1.3.0-preview] - 2019-04-24

NEW FEATURES
* Updated documentation
* Add Python project settings in Unity
* Improved support for installing on Mac and Linux
* Improved logging to help troubleshooting
* Include RPyC and dependencies in Package
* Add option to use different Python on client

## [1.2.0-preview] - 2019-03-13

NEW FEATURES
* Automatically adding Python/site-packages to the PYTHONPATH for the current project and the Python packages
* Added ability to log the Python client messages into a file
* More robust reconnection on domain reload

## [1.1.4-preview] - 2018-12-21

NEW FEATURES
* Added a sample: PySideExample
* Added documentation for In-Process and Out-of-Process APIs
* Better exception logging on the client when an exception is raised on init
* Better error messages when the Python installation is not valid
* RPyC client now automatically starts on server start

## [1.1.3-preview] - 2018-12-14

NEW FEATURES
- This version provides tidier assemblies and APIs

## [1.1.2-preview] - 2018-12-07
NEW FEATURES
- Added a Python example using the RPyC architecture and PySide in the client process
- The RPyC client process now terminates when Unity exits
- The RPyC client can now be stopped and restarted
- Better logging of Python exceptions in the Unity console
- Improved error message when the Python interpreter is not properly configured
- Added a Python/Debug menu that allows to
- - Start the RPyC server
- - Stop the RPyC server
- - Start the RPyC client
- - Start the RPyC server and the client

## [1.1.1-preview] - 2018-11-26

NEW FEATURES
- Added methods to PythonRunner for 
  - Running Python on the RPyC client
  - Starting and stopping the RPyC server
  - Preventing .pyc files from being generated

FIXES
- Fixed deadlocks when closing the RPyC server and client

## [1.1.0-preview] - 2018-11-13

NEW FEATURES
- Added RPyC architecture (under Python/site-packages/unity_rpyc)
- Updated Python for .NET to include:
  - A fix to a crash when finalizing the Python interpreter on domain unload
  - A C# callback on Python for .NET shutdown

KNOWN ISSUES
- There might be scenarios that still crash/hang Unity when running Python after reloading assemblies. 
  - If your tools are affected by domain reload, consider using the RPyC architecture. Refer to the documentation for an example on how to use the RPyC architecture.

## [1.0.0] - 2018-10-05

NEW FEATURES
- added Python support in Unity for Windows and Mac

KNOWN ISSUES
- Trying to call UnityEngine.Debug.Log (or its variants) with a python string that contains non-ANSI characters, will cause the following error: `Python.Runtime.PythonException: TypeError : No method matches given arguments for Log`. For example this can happen on a French language version of Windows when a socket connection fails with `error: [Errno 10061] Aucune connexion n’a pu être établie car l’ordinateur cible l’a expressément refusée`.
