"""
This is an example of using the Python Scripting API to create a
PySide2 window that has a live link with Unity.

This example shows:
* how to create a QApplication safely
* how to run its main event loop
* how to create a PySide2 dialog in that QApplication
* how to call the Unity API safely from within a PySide2 dialog
* how to push events from the Unity API safely into a PySide2 dialog

What the example window does is it shows the available cameras in the scene and
updates the list automatically as new cameras are added.  When the user selects
a camera in the PySide view and clicks "Use Camera", Unity switches to using
that camera in the Scene View.
"""

import logging
import os
import sys
import traceback

from scheduling import exec_on_main_thread, exec_on_main_thread_async

# This is the C# System.dll not the Python sys module.
import System

import UnityEngine
import UnityEditor
from UnityEditor.Scripting.Python.Samples import PySideExample

# Get PySide2
try:
    from PySide2 import QtCore, QtUiTools, QtWidgets
except ModuleNotFoundError:
    UnityEngine.Debug.LogError("Please install PySide2 to use the PySideExample")
    raise

### Globals
_PYSIDE_UI = None
_qApp = None

### UI class
class PySideTestUI():
    # If we use slots we need to include weakref support
    __slots__ = [ '_dialog', '__weakref__' ]

    def __init__(self):
        self._dialog = None

        try:
            # Create the dialog from our .ui file
            ui_path = os.path.join(os.path.dirname(__file__), 'PySideExample.ui')
            self._dialog = self.load_ui_widget(ui_path.replace("\\", "/"))

            # Set up our data.
            self.populate_camera_list()

            # Show the dialog
            self._dialog.show()

        except:
            log('Got an exception while creating the dialog.', logging.ERROR, traceback.format_exc())
            raise

    # Functions that call into C# must run on the main thread.
    # Use the exec_on_main_thread decorator to achieve that.
    #
    # Warning: make sure the main thread isn't waiting on the current thread,
    # or this would cause a deadlock!
    @exec_on_main_thread
    def populate_camera_list(self):
        """
        Populates the list of cameras by asking Unity for all the cameras.
        """
        cameras = [x.name for x in UnityEngine.Camera.allCameras]

        # Populate the list
        list_widget = self._dialog.listWidget
        list_widget.clear()
        for cam in cameras:
            list_widget.addItem(cam)

        log("Cameras list successfully populated")

    @exec_on_main_thread
    def use_camera(self):
        if not self._dialog:
            return
        # Get the selected camera name
        selected_items = self._dialog.listWidget.selectedItems()
        if len(selected_items) != 1:
            return

        try:
            camera = UnityEngine.GameObject.Find('{}'.format(selected_items[0].text()))

            # Apply camera selection
            self.select_camera(camera)

            UnityEditor.EditorApplication.ExecuteMenuItem('GameObject/Align View to Selected')
        except:
            log('Got an exception trying to use the camera:{}'.format(selected_items[0].text()), logging.ERROR, traceback.format_exc())
            raise

    def load_ui_widget(self, uifilename, parent=None):
        # Load the UI made in Qt Designer
        # As seen on Stack Overflow: https://stackoverflow.com/a/18293756
        loader = QtUiTools.QUiLoader()
        uifile = QtCore.QFile(uifilename)
        uifile.open(QtCore.QFile.ReadOnly)
        ui = loader.load(uifile, parent)
        uifile.close()

        # Connect the Button's signal
        ui.useCameraButton.clicked.connect(self.use_camera)

        return ui

    @exec_on_main_thread
    def select_camera(self, camera):
        selList = [camera.GetInstanceID()]
        selection = System.Array[int](selList)
        UnityEditor.Selection.instanceIDs = selection

# For logging, we don't need to wait for the log to occur before returning
# control: we can asynchronously execute it.
@exec_on_main_thread_async
def log(what, level=logging.INFO, traceback=None):
    """
    Short-hand method to log a message in Unity. At logging.DEBUG it prints
    into the Editor's log file (https://docs.unity3d.com/Manual/LogFiles.html)
    At level logging.INFO, logging.WARN and logging.ERROR it uses
    UnityEngine.Debug.Log, UnityEngine.Debug.LogWarning and
    UnityEngine.Debug.LogError, respectively.
    """
    message = "{}".format(what)
    if traceback:
        message += "\nStack:\n{}".format(traceback)

    if level == logging.DEBUG:
        System.Console.WriteLine(message)
    elif level == logging.INFO:
        UnityEngine.Debug.Log(message)
    elif level == logging.WARN:
        UnityEngine.Debug.LogWarning(message)
    else:
        UnityEngine.Debug.LogError(message)

def create_or_reinitialize():
    # Create the QApplication if not already created
    global _qApp
    if not _qApp:
        # Important: on mac, disable the native menu bar handling -- otherwise
        # the Unity menus will disappear and you risk a crash when Unity exits.
        QtWidgets.QApplication.setAttribute(QtCore.Qt.AA_MacPluginApplication)
        _qApp = QtWidgets.QApplication([sys.executable])

    # Create the camera chooser window if not already created; show it if it was
    # previously created but was hidden.
    global _PYSIDE_UI
    if not _PYSIDE_UI:
        _PYSIDE_UI = PySideTestUI()
    else:
        _PYSIDE_UI.populate_camera_list()
        _PYSIDE_UI._dialog.show()

def update_camera_list():
    if not _PYSIDE_UI:
        return
    _PYSIDE_UI.populate_camera_list()

def on_update():
    QtWidgets.QApplication.processEvents()
