#if UNITY_2019_1_OR_NEWER

using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Scripting.Python
{
    [System.Serializable]
    public class PythonConsoleWindow : EditorWindow
    {
#region Window

        // Menu item which calls the window.
        [MenuItem("Window/General/Python Console")]
        public static void ShowWindow()
        {
            s_window = GetWindow<PythonConsoleWindow>();
            s_window.titleContent = new GUIContent("Python Script Editor");

            // Handle window sizing.
            s_window.minSize = new Vector2(550, 300);
        }

        // implementation based off of https://stackoverflow.com/questions/384042/can-i-limit-the-depth-of-a-generic-stack
        private class DropOutStack<T>
        {
            private T[] items;
            private int top = 0;
            private int currentSize = 0;

            public DropOutStack(int capacity)
            {
                items = new T[capacity];
            }

            public void Push(T item)
            {
                items[top] = item;
                top = (top + 1) % items.Length;
                currentSize = Math.Min(currentSize+1, items.Length);
            }

            public T Pop()
            {
                if(IsEmpty())
                {
                    throw new InvalidOperationException("Popping from an empty stack");
                }
                top = (items.Length + top - 1) % items.Length;
                currentSize = currentSize - 1;
                return items[top];
            }

            public void Clear()
            {
                Array.Clear(items, 0, items.Length);
                top = 0;
                currentSize = 0;
            }

            public bool IsEmpty()
            {
                return currentSize <= 0;
            }
        }

        private bool m_performedUndoRedo = false;
        private const int m_maxStackSize = 100;
        private DropOutStack<string> m_undoStack = new DropOutStack<string>(m_maxStackSize);
        private DropOutStack<string> m_redoStack = new DropOutStack<string>(m_maxStackSize);

        private void PerformUndoRedo(string newText)
        {
            m_textFieldCode.SetValueWithoutNotify(newText);
            // make sure the cursor stays at the right position
            var index = m_code==null ? 0 : m_code.Length;
            m_textFieldCode.SelectRange(index, index);
            m_textFieldCode.MarkDirtyRepaint();
            m_performedUndoRedo = true;
        }

        private void PerformUndo()
        {
            if (m_undoStack.IsEmpty())
            {
                // nothing to do
                return;
            }
            m_redoStack.Push(m_code);
            m_code = m_undoStack.Pop();
            PerformUndoRedo(m_code);
        }

        private void PerformRedo()
        {
            if (m_redoStack.IsEmpty())
            {
                // nothing to do
                return;
            }
            m_undoStack.Push(m_code);
            m_code = m_redoStack.Pop();
            PerformUndoRedo(m_code);
        }

        public void OnEnable()
        {
            // Creation and assembly of the window.

            var root = rootVisualElement;

            // Construct toolbar. // Currently not handled by uxml (2019.1.1f1).
            var toolbar = new Toolbar();
            root.Add(toolbar);
            var tbButtonLoad = new ToolbarButton { text = "Load" };
            toolbar.Add(tbButtonLoad);
            var tbButtonSave = new ToolbarButton { text = "Save" };
            toolbar.Add(tbButtonSave);
            var tbButtonSaveMenu = new ToolbarButton { text = "Save & Create Shortcut" };
            toolbar.Add(tbButtonSaveMenu);
            var tbSpacer = new ToolbarSpacer();
            toolbar.Add(tbSpacer);
            var tbButtonRun = new ToolbarButton { text = "Execute" };
            toolbar.Add(tbButtonRun);
            var tbSpacer2 = new ToolbarSpacer();
            toolbar.Add(tbSpacer2);
            var tbButtonClearCode = new ToolbarButton { text = "Clear Code" };
            toolbar.Add(tbButtonClearCode);
            var tbButtonClearOutput = new ToolbarButton { text = "Clear Output" };
            toolbar.Add(tbButtonClearOutput);
            var tbButtonClearAll = new ToolbarButton { text = "Clear All" };
            toolbar.Add(tbButtonClearAll);


            // Assemble and construct visual tree.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.scripting.python/Styles/pythonconsole_uxml.uxml");
            visualTree.CloneTree(root);
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.scripting.python/Styles/pythonconsole_uss.uss"));


            // Fetch references to UXML objects (UQuery).
            m_textFieldCode = root.Query<TextField>("textAreaCode").First();
            m_textFieldOutput = root.Query<TextField>("textAreaOutput").First();

            m_holderofOutputTextField = root.Query<ScrollView>("holderOutput").First();

            m_scrollerOutput = root.Query<Scroller>(className: "unity-scroller--vertical").First();
            m_scrollerCode = root.Query<Scroller>(className: "unity-scroller--vertical").Last();

            // this field is serialized, but not the stacks. reset it explicitly.
            m_undoGroupCount = 0;

            // Handle reserialization.
            m_textFieldCode.SetValueWithoutNotify(m_code);
            m_textFieldOutput.value = m_outputContents;

            // Set up the Undo handling
            // Use the event to add to the undo stack and collapse the stack to 
            // give a feeling closer to a real text editor undo
            m_textFieldCode.RegisterCallback<ChangeEvent<string>>(OnCodeInput);

            // Implement event handlers.
            m_textFieldCode.Q(TextField.textInputUssName).RegisterCallback<KeyDownEvent>(OnExecute);
            tbButtonLoad.RegisterCallback<MouseUpEvent>(OnLoad);
            tbButtonSave.RegisterCallback<MouseUpEvent>(OnSave);
            tbButtonSaveMenu.RegisterCallback<MouseUpEvent>(OnSaveShortcut);

            tbButtonRun.RegisterCallback<MouseUpEvent>(OnExecute);

            tbButtonClearCode.RegisterCallback<MouseUpEvent>(OnClearCode);
            tbButtonClearOutput.RegisterCallback<MouseUpEvent>(OnClearOutput);
            tbButtonClearAll.RegisterCallback<MouseUpEvent>(OnClearAll);

            // Also assign to the static variable here. On some occasions,
            // like domain reloads, the value is lost and if the window is
            // already showing, ShowWindow() won't be called, and code output
            // will never go to the console output.
            s_window = this;
        }
#endregion


#region Class Variables

        // Keep a reference on the Window for two reasons:
        // 1. Better performance
        // 2. AddToOutput is called from thread other than the main thread
        //    and it triggers a name search, which can only be done in the 
        //    main thread
        internal static PythonConsoleWindow s_window = null;

        TextField m_textFieldCode;
        TextField m_textFieldOutput;
        ScrollView m_holderofOutputTextField;

        [SerializeField]
        string m_code = "";

        // To collapse the undo stack
        static float lastEditTime = 0;
        static int groupid;

        [SerializeField]
        internal string m_outputContents;

        // Sizing utility variables
        const int k_borderBuffer_WindowBottom = 50;
        const int k_borderBuffer_SplitHandle = 2;

        // Too much text sent into a TextField throws an error:
        // "MakeTextMeshHandle: text is too long and generates too many vertices"
        // It happens when the text size is above ~11000
        // Set a limit to 10000 to have a safety margin
        const int k_kMaxOutputLength = 10000;

        Scroller m_scrollerOutput;
        Scroller m_scrollerCode;
#endregion

        #region Event Functions

        int m_undoGroupCount = 0;
        
        // Text is inputed into the Code text area.
        // Used to collapse the undo stack so it's (mostly) consistent with a text editor.
        void OnCodeInput(ChangeEvent<string> e)
        {
            if (m_performedUndoRedo)
            {
                m_redoStack.Clear();
                m_undoGroupCount = 0;
                m_performedUndoRedo = false;
            }

            m_undoStack.Push(m_code);
            m_code = e.newValue;

            float curTime = Time.realtimeSinceStartup;
            // .333 feels right; may need more adjustments
            if ((curTime - lastEditTime) < 0.333f)
            {
                m_undoGroupCount++;
            }
            else
            {
                // collapse undo operations after 0.333 seconds.
                for(int i = 0; i < m_undoGroupCount; i++)
                {
                    m_undoStack.Pop();
                }
                m_undoGroupCount = 0;
            }
            lastEditTime = curTime;
        }
        
        const string k_undoShortcutBindingID = "Main Menu/Edit/Undo";
        const string k_redoShortcutBindingID = "Main Menu/Edit/Redo";

        private bool IsShortcutPressed(string shortcutID, KeyDownEvent e)
        {
            var binding = ShortcutManagement.ShortcutManager.instance.GetShortcutBinding(shortcutID);
            foreach (var combo in binding.keyCombinationSequence)
            {
                if (combo.action == e.actionKey &&
                    combo.alt == e.altKey &&
                    combo.keyCode == e.keyCode &&
                    combo.shift == e.shiftKey)
                {
                    return true;
                }
            }
            return false;
        }

        // Were the right key pressed? This variable is used by the subsequent code to keep track of the two events generated on key press.
        bool m_wereActionEnterPressed;
        // Key(s) are pressed while the Code area is in focus. 
        void OnExecute(KeyDownEvent e)
        {
            // Verify that the Action (Control/Command) and Return (Enter/KeypadEnter) keys were pressed, or that the KeypadEnter was pressed.
            // This 'catches' the first event. This event carries the keyCode(s), but no character information.
            // Here we execute the Python code. The textField itself is left untouched.
            if (e.keyCode == KeyCode.KeypadEnter || (e.actionKey == true && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)))
            {
                m_wereActionEnterPressed = true;
                if (!string.IsNullOrEmpty(GetSelectedCode()))
                {
                    PartialExecute();
                }
                else
                {
                    ExecuteAll();
                    e.PreventDefault();
                }
            }

            // If the right keys were pressed, prevent the KeyDownEvent's default behaviour.
            // This 'catches' the second event. It carries the character information (in this case, "\n"), but has a keyCode of None.
            // Since it is responsible for writing into the textField, we here prevent its default proceedings.
            if (e.keyCode == KeyCode.None && m_wereActionEnterPressed == true)
            {
                e.PreventDefault();
                m_wereActionEnterPressed = false;
            }

            if(IsShortcutPressed(k_undoShortcutBindingID, e))
            {
                PerformUndo();
                // This is MY event!
                // Nobody else is allowed to have it.
                e.PreventDefault();
                e.StopPropagation();
                return;
            }

            if(IsShortcutPressed(k_redoShortcutBindingID, e))
            {
                PerformRedo();
                // This is MY event!
                // Nobody else is allowed to have it.
                e.PreventDefault();
                e.StopPropagation();
                return;
            }
        }

        // 'Execute' is pressed.
        void OnExecute(MouseUpEvent e)
        {
            ExecuteAll();
        }

        // 'Load' is pressed.
        void OnLoad(MouseUpEvent e)
        {
            // Let the user select a file.
            var fullFilePath = EditorUtility.OpenFilePanel("Load Python Script", "", "py");
                
            // Verify that the resulting file string is not empty (in case the user hit 'cancel'). If it is, return.
            if (string.IsNullOrEmpty(fullFilePath))
            {
                return;
            }

            // Once a file has been chosen, clear the console's current content.
            OnClearCode(e);
                
            // Read and copy the file's content
            var code = System.IO.File.ReadAllText(fullFilePath);
            // And set the text area to it.
            SetCode(code);
        }

        // 'Save' is pressed.
        void OnSave(MouseUpEvent e)
        {
            // Let the user select a save path.
            var savePath = EditorUtility.SaveFilePanelInProject("Save Current Script", "new_python_script", "py", "Save location of the current script.");

            //Make sure it is valid (the user did not cancel the save menu).
            if (!string.IsNullOrEmpty(savePath))
            {
                // Write current console contents to file.
                System.IO.File.WriteAllText(savePath, GetCode());
            }
        }

        // "Save & Create Shortcut' is pressed.
        void OnSaveShortcut(MouseUpEvent e)
        {
            CreateMenuItemWindow.ShowWindow(GetCode());
        }

        // 'Clear Code' is pressed.
        void OnClearCode(MouseUpEvent e)
        {
            // Update the textfield's value.
            SetCode("");
        }

        // 'Clear Output' is pressed.
        void OnClearOutput(MouseUpEvent e)
        {
            // Set the current content variable to null.
            m_outputContents = "";
            // Update the textfield's value.
            m_textFieldOutput.value = m_outputContents;
        }

        // 'Clear All' is pressed.
        void OnClearAll(MouseUpEvent e)
        {
            OnClearCode(e);
            OnClearOutput(e);
        }
#endregion


#region Utility Functions
        
        // Fetch and return the current console content as a string.
        string GetCode()
        {
            return m_textFieldCode.value;
        }

        void SetCode(string code)
        {
            m_textFieldCode.value = code;
        }

        // Fetch and return the current code selection as a string.
        string GetSelectedCode()
        {
#if UNITY_2022_1_OR_NEWER
            var selectionData = m_textFieldCode.textSelection;
            return m_textFieldCode.text.Substring(selectionData.cursorIndex, selectionData.selectIndex - selectionData.cursorIndex);
#else
            // The current text selection of a TextField is not available through the public API in Unity 2019.1.
            // We can optain it through its TextEditor, which itself must be accessed by reflection.
            var textEditorProperty = m_textFieldCode.GetType().GetProperty("editorEngine", BindingFlags.Instance | BindingFlags.NonPublic);
            var textEditor = textEditorProperty.GetValue(m_textFieldCode) as TextEditor;

            string selectedText = textEditor.SelectedText;
            return selectedText;
#endif
        }

        // Set the output field's displayed content to the associated variable.
        void SetOutputField()
        {
            int outputLength = m_outputContents.Length;
            if (outputLength > k_kMaxOutputLength)
            {
                m_textFieldOutput.value = m_outputContents.Substring(outputLength - k_kMaxOutputLength, k_kMaxOutputLength);
            }
            else
            {
                m_textFieldOutput.value = m_outputContents;
            }
        }

        // Add the inputed string to the output content.
        static public void AddToOutput(string input)
        {
            if(s_window)
            {
                s_window.InternalAddToOutput(input);
            }
        }

        void InternalAddToOutput(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                m_outputContents += input;
                SetOutputField();
            }
        }

        void Execute (string code)
        {
            PythonRunner.RunString(code, "__main__");
        }

        // Execute only the current selection.
        void PartialExecute()
        {
            Execute(GetSelectedCode());
        }

        void ExecuteAll ()
        {
            Execute(GetCode());
        }
       
#endregion

#region Update Functions

        // For code review: should this use the simple Update() ? Either of them don't seem to make a difference in how quickly the textField is redrawn.
        private void OnGUI()
        {
            // Handle current size of the Code text field.
            var textFieldCode_CurrentSize = rootVisualElement.contentRect.height - m_holderofOutputTextField.contentRect.height - k_borderBuffer_WindowBottom;
            m_textFieldCode.style.minHeight = textFieldCode_CurrentSize;

            // Handle current minimum size of the Output text field.
            var textFieldOutput_CurrentSize = m_holderofOutputTextField.contentRect.height - k_borderBuffer_SplitHandle;
            m_textFieldOutput.style.minHeight = textFieldOutput_CurrentSize;
            m_holderofOutputTextField.style.minHeight = textFieldOutput_CurrentSize;

            // Ajust vertical scroller sizes.
            // This is done pretty roughly atm. Need to optimize.
            m_scrollerOutput.style.bottom = 7;
            m_scrollerCode.style.bottom = 7;
        }
#endregion
    }

    // This is the pop up used in the creation of menu shortcuts upon script saving.
    [System.Serializable]
    public class CreateMenuItemWindow : EditorWindow, IDisposable
    {
        #region Class Variables
        TextField m_textfieldMenuName;
        IMGUIContainer m_helpboxContainer;
        Button m_buttonCommitMenuName;

        private bool m_isMenuNameValid = true;
        private string m_codeToSave;
        #endregion

        #region Window
        public static void ShowWindow(string codeToSave)
        {
            CreateMenuItemWindow window = CreateInstance(typeof(CreateMenuItemWindow)) as CreateMenuItemWindow;
            window.titleContent.text = "Create Menu Shortcut";
            window.ShowUtility();

            // Handle window sizing.
            window.minSize = new Vector2(540, 86);
            window.maxSize = new Vector2(1000, 86);

            // Local storage of the code to save.
            window.m_codeToSave = codeToSave;
        }

        public void OnEnable()
        {
            // Creation and assembly of the window.
            var root = rootVisualElement;

            // Construct its contents.
            m_textfieldMenuName = new TextField { label = "Submenu Name: ",
                                                value = "Python Scripts/New Python Script" };
            root.Add(m_textfieldMenuName);
            m_helpboxContainer = (new IMGUIContainer(OnValidation));
            root.Add(m_helpboxContainer);
            m_buttonCommitMenuName = new Button { text = "Create" };
            root.Add(m_buttonCommitMenuName);

            // Assign style sheet.
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.scripting.python/Styles/pythonpopup_uss.uss"));

            // Implement event handles.
            m_textfieldMenuName.RegisterCallback<ChangeEvent<string>>(OnPathEdit);
            m_buttonCommitMenuName.RegisterCallback<MouseUpEvent>(OnCommit);
        }

        // Needed to display a help box corresponding to those used in the default Unity UI. 
        // Additionally handles whether the button should be active.
        private void OnValidation()
        {
            if (!m_isMenuNameValid)
            {
                EditorGUILayout.HelpBox("The current menu input is not valid.", MessageType.Error);
                m_buttonCommitMenuName.SetEnabled(false);
            }
            else
            {
                EditorGUILayout.HelpBox("Note: the menu must be at least 2 levels deep. The option itself must have a unique name.", MessageType.Info);
                m_buttonCommitMenuName.SetEnabled(true);
            }
        }
        void OnInspectorUpdate()
        {
            Repaint();
        }

        #endregion

        #region Utility Functions
        void OnPathEdit(ChangeEvent<string> e)
        {
            m_textfieldMenuName.value = m_textfieldMenuName.value.Replace("\\", "/");
            m_textfieldMenuName.value = m_textfieldMenuName.value.Replace("//", "/");

            m_isMenuNameValid = ValidateMenuName();
        }

        void OnCommit(MouseUpEvent e)
        {
            if (ValidateMenuName())
            {
                // Let the user select a save path.
                string pySavePath = EditorUtility.SaveFilePanelInProject("Save Current Script & Create Menu Shortcut", "new_python_script", "py", "Save location of the current script, as well as that of its menu shortcut.");

                //Make sure it is valid (the user did not cancel the save menu).
                if (!string.IsNullOrEmpty(pySavePath))
                {
                    // Write current console contents to file.
                    System.IO.File.WriteAllText(pySavePath, m_codeToSave);

                    // Create the associated menu item's script file.
                    WriteShortcutFile(pySavePath, m_textfieldMenuName.value);
                }
                else
                    this.Close();
            }
        }

        bool ValidateMenuName()
        {
            string value = m_textfieldMenuName.value;
            string[] namedLevels = m_textfieldMenuName.value.Split('/');

            // Verify that the menu name/path contains at least one sublevel, and does not begin nor end with a slash.
            if (!value.Contains("/")) { return false; }
            if (value[0] == '/') { return false; }
            if (value[value.Length - 1] == '/') { return false; }

            // Verify that each level has an adequate name.
            foreach (string subName in namedLevels)
            {
                string cleanSubName = Regex.Replace(subName, "[^A-Za-z0-9]", "");
                if (string.IsNullOrEmpty(cleanSubName)) { return false; }
            }

            // Verify that the corresponding script name would not be null.
            if (string.IsNullOrEmpty(GetScriptName(value))) { return false; }

            return true;
        }

        // Handle script name.
        string GetScriptName(string menuName)
        {
            int i = menuName.LastIndexOf('/') + 1;
            string scriptName = menuName.Substring(i);
            scriptName = Regex.Replace(scriptName, @"[\W]", "");

            return scriptName;
        }

        // Rewrite the template menu shortcut script as needed.
        void WriteShortcutFile(string pySavePath, string menuName)
        {
            // Convert m_savePath file from Python to C#.
            string csSavePath = pySavePath.Replace(".py", ".cs");

            // Get scriptName from menuName.
            string scriptName = GetScriptName(menuName);

            // Create className from scriptName.
            string className = $"MenuItem_{scriptName}_Class";

            string scriptContents = "using UnityEditor;\n"
                                  + "using UnityEditor.Scripting.Python;\n"
                                  + "\npublic class " + className + "\n"
                                  + "{\n"
                                  + "   [MenuItem(\"" + menuName + "\")]\n"
                                  + "   public static void " + scriptName + "()\n"
                                  + "   {\n"
                                  + "       PythonRunner.RunFile(\"" + pySavePath + "\");\n"
                                  + "       }\n"
                                  + "};\n";

            try
            {
                // Write the resulting .cs file in the same location as the saved Python script.
                System.IO.File.WriteAllText(csSavePath, scriptContents);

                // Reset and close the popup window.
                m_textfieldMenuName.value = "Python Scripts/New Python Script";
                this.Close();

                AssetDatabase.Refresh();
            }

            catch (Exception e)
            {
                Debug.LogError("Failure to create the menu item for this Python script.\n" + e.Message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_helpboxContainer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
#endif
