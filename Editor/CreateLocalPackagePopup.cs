using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreateLocalPackagePopup : EditorWindow
{
    public static void Init(string destination, string[] localPackageRootFolders = null, LocalDependencySuggestion[] dependencyOptions = null, string packageName = "New Package", string developerName = "developer")
    {
        //var window = GetWindow<CreateLocalPackagePopup>();
        var window = ScriptableObject.CreateInstance<CreateLocalPackagePopup>();
        window.titleContent = new GUIContent("Create New Local Package");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 450, 342);
        window.localPackageFolderDestination = destination;
        if (localPackageRootFolders != null)
        {
            window.localPackageRootFolders = localPackageRootFolders;
            window.localPackageRootFolderLabels = new string[localPackageRootFolders.Length];
            for (int i = 0; i < localPackageRootFolders.Length; i++)
            {
                window.localPackageRootFolderLabels[i] = localPackageRootFolders[i].Replace('/', '>');
            }
        }
        if (dependencyOptions != null)
        {
            window.dependencyOptions = dependencyOptions;
            window.dependencyOptionLabels = new string[dependencyOptions.Length];
            for (int i = 0; i < dependencyOptions.Length; i++)
            {
                window.dependencyOptionLabels[i] = window.localPackageRootFolderLabels[dependencyOptions[i].localPackageRootFolderIndex] +
                    "/" + dependencyOptions[i].packageName;
            }
        }
        window.packageComName = "com." + developerName + "." + packageName.Replace(" ", "").ToLower();
        window.SetNames(packageName);
        window.ShowUtility();
    }

    public struct LocalDependencySuggestion
    {
        public int localPackageRootFolderIndex;
        public string packageName;
        public string version;
    }

    [System.Serializable]
    public class dependencyAndVersion
    {
        public string packageName;
        public string version = "1.0.0";
    }

    void SetNames(string folderName)
    {
        this.folderName = folderName;
        int lastComNamePeriod = packageComName.LastIndexOf(".");
        if (lastComNamePeriod == -1)
        {
            packageComName = "com.developer." + folderName.Replace(" ", "").ToLower();
        }
        else
        {
            packageComName = packageComName.Substring(0, lastComNamePeriod + 1) + folderName.Replace(" ", "").ToLower();
        }
        packageDisplayName = folderName;
        packageAssemblyName = folderName.Replace(" ", "");
    }

    Vector2 scrollPosition;

    string[] localPackageRootFolders;
    string[] localPackageRootFolderLabels;
    LocalDependencySuggestion[] dependencyOptions;
    string[] dependencyOptionLabels;
    bool createNewFolder;
    string[] versionDropdownOptions = new string[] { "1.0.0", "0.0.1" };

    public string localPackageFolderDestination;
    public string folderName;
    public string packageComName;
    public string packageDisplayName;
    [Tooltip("this is allowed to be uppercase, but without spaces")]
    public string packageAssemblyName;
    public string packageVersion = "1.0.0";
    public string packageDescription;
    public string minimumUnityVersion = "2017.2";
    public List<dependencyAndVersion> packageDependencies = new List<dependencyAndVersion>();
    public bool authorSection = true;
    public string authorName;
    public string authorEmail;
    public string authorUrl;
    public bool editorAssemblyFolder = true;
    public bool installPackageAfterCreated = true;

    void OnGUI()
    {
        SerializedObject serializedObject = new SerializedObject(this);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(localPackageFolderDestination)), true);
        int chosenFolderDropdownIndex = -1;
        chosenFolderDropdownIndex = EditorGUILayout.Popup(new GUIContent(""), chosenFolderDropdownIndex, localPackageRootFolderLabels, GUILayout.Width(20));
        if (chosenFolderDropdownIndex != -1)
        {
            // Debug.Log("chose " + localPackageRootFolders[chosenFolderDropdownIndex]);
            serializedObject.FindProperty(nameof(localPackageFolderDestination)).stringValue = localPackageRootFolders[chosenFolderDropdownIndex];
        }
        if (EditorGUI.EndChangeCheck())
        {
            // Debug.Log("edited directory field");
            // if folder does not exist, set createNewFolder
            var directory = serializedObject.FindProperty(nameof(localPackageFolderDestination)).stringValue;
            createNewFolder = !Directory.Exists(directory);
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(folderName)), true);
        if (GUILayout.Button("Update names", GUILayout.Width(100)))
        {
            UnityEditor.Undo.RecordObject(this, "update names");
            SetNames(folderName);
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageComName)), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageDisplayName)), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageAssemblyName)), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageDescription)), true);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageVersion)), true);
        int chosenVersionDropdownIndex = -1;
        chosenVersionDropdownIndex = EditorGUILayout.Popup(new GUIContent(""), chosenVersionDropdownIndex, versionDropdownOptions, GUILayout.Width(20));
        if (chosenVersionDropdownIndex != -1)
        {
            // Debug.Log("chose " + localPackageRootFolders[chosenVersionDropdownIndex]);
            serializedObject.FindProperty(nameof(packageVersion)).stringValue = versionDropdownOptions[chosenVersionDropdownIndex];
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(minimumUnityVersion)), true);

        // dependencies
        var dependenciesProperty = serializedObject.FindProperty(nameof(packageDependencies));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Dependencies");
        if (GUILayout.Button("Add Dependency"))
        {
            // add empty fields
            dependenciesProperty.InsertArrayElementAtIndex(dependenciesProperty.arraySize);
            var addedArrayElementProperty = dependenciesProperty.GetArrayElementAtIndex(dependenciesProperty.arraySize - 1);
            var versionProperty = addedArrayElementProperty.FindPropertyRelative(nameof(dependencyAndVersion.version));
            versionProperty.stringValue = "1.0.0";
        }
        // add dependency dropdown with selection from provided suggestions
        int chosenDependencyDropdownIndex = -1;
        chosenDependencyDropdownIndex = EditorGUILayout.Popup(new GUIContent(""), chosenDependencyDropdownIndex, dependencyOptionLabels, GUILayout.Width(20));
        if (chosenDependencyDropdownIndex != -1)
        {
            // Debug.Log("chose " + dependencyOptionLabels[chosenDependencyDropdownIndex]);
            // add selected dependency
            dependenciesProperty.InsertArrayElementAtIndex(dependenciesProperty.arraySize);
            var addedArrayElementProperty = dependenciesProperty.GetArrayElementAtIndex(dependenciesProperty.arraySize - 1);
            var versionProperty = addedArrayElementProperty.FindPropertyRelative(nameof(dependencyAndVersion.version));
            versionProperty.stringValue = dependencyOptions[chosenDependencyDropdownIndex].version;
            var dependencyNameProperty = addedArrayElementProperty.FindPropertyRelative(nameof(dependencyAndVersion.packageName));
            dependencyNameProperty.stringValue = dependencyOptions[chosenDependencyDropdownIndex].packageName;
        }
        EditorGUILayout.EndHorizontal();
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel++;
        // list of name and version fields, with remove button
        for (int i = 0; i < dependenciesProperty.arraySize; i++)
        {
            var dependencyNameProperty = dependenciesProperty.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(dependencyAndVersion.packageName));
            var versionProperty = dependenciesProperty.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(dependencyAndVersion.version));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(dependencyNameProperty);
            EditorGUILayout.PropertyField(versionProperty, new GUIContent(""), GUILayout.Width(100));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                dependenciesProperty.DeleteArrayElementAtIndex(i);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel = indent;
        // EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(packageDependencies)), true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorSection)), true);
        if (authorSection)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorName)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorEmail)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorUrl)), true);
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(editorAssemblyFolder)), true);

        EditorGUILayout.BeginHorizontal();
        var guiEnabled = GUI.enabled;
        // choosing a folder that does not exist is not the same as a folder that is not in the local package root folders
        // but covers the likely case for this for now
        bool canInstallFromDestinationFolder = !createNewFolder;
        GUI.enabled &= canInstallFromDestinationFolder; // if canInstallFromFolder is false, GUI.enabled will become false
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(installPackageAfterCreated)), true);
        GUI.enabled = guiEnabled;
        if (createNewFolder)
        {
            var warningLabel = new GUIContent("must be a local package helper root dierectory",
                "Can only install packages created in directories known to the local package helper.\n" +
                "Add this directory to the local package root folders in order to install the created package.");
            EditorGUILayout.LabelField(warningLabel);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Create Package"))
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                if (createNewFolder)
                {
                    Directory.CreateDirectory(localPackageFolderDestination);
                }
                Directory.SetCurrentDirectory(localPackageFolderDestination);
                Directory.CreateDirectory(folderName);

                //Write package.json file
                string authorSectionText =
@"    ""author"": {
        ""name"": """ + authorName + @""",
        ""email"": """ + authorEmail + @""",
        ""url"": """ + authorUrl + @"""
    }
";
                string dependencies = "";
                //com.moletotem.emptypackage":"1.0.0"},
                for (int i = 0; i < packageDependencies.Count; i++)
                {
                    dependencies += "\"" + packageDependencies[i].packageName + "\":\"" + packageDependencies[i].version + "\"";
                    if (i < packageDependencies.Count - 1)
                    {
                        dependencies += ",";
                    }
                }
                string packageJson =
@"{
    ""name"": """ + packageComName + @""",
    ""version"": """ + packageVersion + @""",
    ""displayName"": """ + packageDisplayName + @""",
    ""description"": """ + packageDescription + @""",
    ""dependencies"": {" + dependencies + @"},
    ""unity"": """ + minimumUnityVersion + @"""" + (authorSection ? "," : "") + @"
" + (authorSection ? authorSectionText : "") + @"}";

                File.WriteAllText(folderName + "/package.json", packageJson);

                //write runtime asmdef file
                Directory.CreateDirectory(folderName + "/Runtime");
                var runtimeAsmdefText =
@"{
    ""name"": """ + packageAssemblyName + @""",
    ""references"": [],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": []
}";
                File.WriteAllText(folderName + "/Runtime/" + packageAssemblyName + ".asmdef", runtimeAsmdefText);

                if (editorAssemblyFolder)
                {
                    //write editor asmdef file
                    Directory.CreateDirectory(folderName + "/Editor");
                    var editorAsmdefText =
    @"{
    ""name"": """ + packageAssemblyName + @".Editor"",
    ""references"": [
        """ + packageAssemblyName + @"""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": []
}";
                    File.WriteAllText(folderName + "/Editor/" + packageAssemblyName + ".Editor.asmdef", editorAsmdefText);
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
                //Debug.Log(logEntry);
            }
            Debug.Log("Saved package \"" + folderName + "\" to " + localPackageFolderDestination + "/" + folderName + "\n" +
            "Refresh package list in Local Package Helper window to install");
            if (installPackageAfterCreated && canInstallFromDestinationFolder)
            {
                LocalPackageHelper.CreatedNewPackage(new string[] { packageComName });
            }
            else
            {
                LocalPackageHelper.CreatedNewPackage(null);
            }
            Close();
        }
        else if (GUILayout.Button("Cancel"))
        {
            Close();
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUILayout.EndScrollView();
    }
}
