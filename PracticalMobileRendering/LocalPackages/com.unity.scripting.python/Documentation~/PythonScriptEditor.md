# Python Script Editor

The easiest way to start using Python Scripting is with the Python Script Editor:

![Launching the Python Script Editor](images/python-console-how.png)

You can type Python statements in the bottom window. Click on the `Execute`
button or press Ctrl-Return to run the selected text.

Python `print` statements will appear in the upper window as well as in the Editor.log.

![Using the Python Script Editor](images/python-console-example.png)

The load and save buttons allow reusing scripts.

`Save & Create Shortcut` adds a menu item to run the current script.

![Save and Create Shortcut](images/python-console-menu-item.png)

It does this by creating two files side by side: a C# file to create the menu
item, and a Python file which is a copy of your script in the Python Script Editor.
The menu item name needs to be unique. You will see an error in the Unity console if
you use an existing menu item name, and you will need to fix the C# file and modify
its `[MenuItem]`.

## Advanced Tool Programming

To implement more complex Python-based tools in Unity, the
[API](inProcessAPI.md) allows you to invoke Python code from C# that runs in
the Unity process.

## Limitations

The output has a limit of 10,000 characters.
