﻿using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CreateLocalPackagePopup : EditorWindow
{
    public static void Init(string destination, string packageName = "New Package", string developerName = "developer")
    {
        //var window = GetWindow<CreateLocalPackagePopup>();
        var window = ScriptableObject.CreateInstance<CreateLocalPackagePopup>();
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 450, 300);
        window.localPackageFolderDestination = destination;
        window.packageComName = "com." + developerName + "." + packageName.Replace(" ", "").ToLower();
        window.SetNames(packageName);
        window.ShowModalUtility();
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

    public string localPackageFolderDestination;
    public string folderName;
    public string packageComName;
    public string packageDisplayName;
    [Tooltip("this is allowed to be uppercase, but without spaces")]
    public string packageAssemblyName;
    public string packageDescription;
    public string minimumUnityVersion = "2017.2";
    public bool authorSection = true;
    public string authorName;
    public string authorEmail;
    public string authorUrl;
    public bool editorAssemblyFolder = true;

    void OnGUI()
    {
        SerializedObject serializedObject = new SerializedObject(this);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(localPackageFolderDestination)), true);
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
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(minimumUnityVersion)), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorSection)), true);
        if (authorSection)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorName)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorEmail)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(authorUrl)), true);
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(editorAssemblyFolder)), true);

        if (GUILayout.Button("Create Package"))
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory("/" + localPackageFolderDestination);
                Directory.CreateDirectory(folderName);

                //Write package.json file
                string authorSectionText =
@"    ""author"": {
        ""name"": """ + authorName + @""",
        ""email"": """ + authorEmail + @""",
        ""url"": """ + authorUrl + @"""
    }
";
                string packageJson =
@"{
    ""name"": """ + packageComName + @""",
    ""version"": ""1.0.0"",
    ""displayName"": """ + packageDisplayName + @""",
    ""description"": """ + packageDescription + @""",
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
            Debug.Log("Saved package \""+folderName+"\" to "+localPackageFolderDestination+"/"+folderName+"\n"+
            "Refresh package list in Local Package Helper window to install");
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
    }
}
