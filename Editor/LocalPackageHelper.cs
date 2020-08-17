using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using UnityEngine;

public class LocalPackageHelper : EditorWindow
{
    [MenuItem("Window/Local Package Helper")]
    static void Init()
    {
        GetWindow<LocalPackageHelper>();
    }

    void OnEnable()
    {
        // get local package folders from text file
        var assetPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("localPackageFolders").FirstOrDefault());
        if (File.Exists(assetPath))
        {
            // Debug.Log("found local packages folder list at: " + assetPath);
            localPackageRootFolders = File.ReadAllLines(assetPath).ToList();
        }
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    void OnDisable()
    {
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
    }

    public void OnAfterAssemblyReload()
    {
        // Debug.Log("After Assembly Reload");
        if (PlayerPrefs.HasKey("AddPackagesQueue"))
        {
            Debug.Log("Restoring install packages queue");
            string loadValue = PlayerPrefs.GetString("AddPackagesQueue");
            Debug.Log(loadValue);
            addPackages = loadValue.Split(',').ToList();
            foreach (var item in addPackages)
            {
                Debug.Log("(LocalPackagesHelper) install " + item);
            }
        }
        if (PlayerPrefs.HasKey("RemovePackagesQueue"))
        {
            Debug.Log("Restoring remove packages queue");
            string loadValue = PlayerPrefs.GetString("RemovePackagesQueue");
            Debug.Log(loadValue);
            removePackages = loadValue.Split(',').ToList();
            foreach (var item in removePackages)
            {
                Debug.Log("(LocalPackagesHelper) remove " + item);
            }
        }
        localPackages = GetPackageInfoForLocalPackageFolders();
        if (ListRequest == null)
        {
            ListRequest = Client.List(true);
            EditorApplication.update += PollInstalledPackagesProgress;
        }
        else
        {
            Debug.Log("(LocalPackagesHelper) polling already in progress");
        }
    }

    [ContextMenuItem("Open folder location", "OpenFolderLocation")]
    public List<string> localPackageRootFolders = new List<string>();

    static ListRequest ListRequest;
    static AddRequest AddPackageRequest;
    static RemoveRequest RemovePackageRequest;
    static List<string> removePackages;
    static List<string> addPackages;
    static List<PackageInfo> localPackages;
    static List<string> installedPackages = new List<string>();

    //UI
    bool[] localPackagesFoldouts = new bool[0];
    List<PackageInfo> selectedPackages = new List<PackageInfo>();
    List<PackageInfo> selectedDependencyPackages = new List<PackageInfo>();
    bool installDependencies = true;
    bool selectingForInstall = true;
    bool foldersModified = false;
    Vector2 scrollPosition;

    public class PackageInfo
    {
        public string name;
        public string displayName;
        public string version;
        //skipped by unity serialization, filled manually
        public Dictionary<string, string> dependencies;
        //filled manually
        public string folder;
        public int rootFolderIndex;
    }

    public class OpenFolderLocationPopup : PopupWindowContent
    {
        public string[] options;

        public override Vector2 GetWindowSize()
        {
            Vector2 size = new Vector2(10, 10);
            for (int i = 0; i < options.Length; i++)
            {
                Vector2 sizeOfLabel = EditorStyles.toolbarButton.CalcSize(new GUIContent(options[i]));
                size.x = Mathf.Max(size.x, sizeOfLabel.x);
                size.y += sizeOfLabel.y;
            }
            return size;
        }

        public override void OnGUI(Rect rect)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (GUILayout.Button(options[i], EditorStyles.toolbarButton))
                {
                    Debug.Log("chose " + options[i] + " i guess this doesn't work, but it looks nice");
                    string path = options[i];

                    // no response
                    // EditorUtility.RevealInFinder(path);

                    // no response
                    // bool openInsidesOfFolder = false;
                    // string macPath = path.Replace("\\", "/"); // mac finder doesn't like backward slashes
                    // if (Directory.Exists(macPath)) // if path requested is a folder, automatically open insides of that folder
                    // {
                    //     openInsidesOfFolder = true;
                    // }
                    // if (!macPath.StartsWith("\""))
                    // {
                    //     macPath = "\"" + macPath;
                    // }
                    // if (!macPath.EndsWith("\""))
                    // {
                    //     macPath = macPath + "\"";
                    // }
                    // string arguments = (openInsidesOfFolder ? "" : "-R ") + macPath;
                    // System.Diagnostics.Process.Start("open", arguments);

                    editorWindow.Close();
                }
            }
        }
    }

    // context menu item on folder list array elements
    void OpenFolderLocation()
    {
        var popupContents = new OpenFolderLocationPopup();
        popupContents.options = localPackageRootFolders.ToArray();
        PopupWindow.Show(position, popupContents);
    }

    void OnGUI()
    {
        SerializedObject serializedObject = new SerializedObject(this);
        var localPackageRootFoldersProp = serializedObject.FindProperty(nameof(localPackageRootFolders));

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.PropertyField(localPackageRootFoldersProp, true);
        if (localPackageRootFoldersProp.isExpanded)
        {
            if (serializedObject.hasModifiedProperties)
            {
                foldersModified = true;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                localPackages = GetPackageInfoForLocalPackageFolders();
                if (ListRequest == null)
                {
                    ListRequest = Client.List(true);
                    EditorApplication.update += PollInstalledPackagesProgress;
                }
                else
                {
                    Debug.Log("polling already in progress");
                }
            }
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && foldersModified;
            {
                if (GUILayout.Button("Save folders"))
                {
                    //get local package folders from text file
                    var assetPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("localPackageFolders").FirstOrDefault());
                    if (File.Exists(assetPath))
                    {
                        Debug.Log(assetPath);
                        File.WriteAllLines(assetPath, localPackageRootFolders);
                        foldersModified = false;
                    }
                    else
                    {
                        Debug.Log("no file \"localPackageFolders\" found");
                        //just save the file somewhere
                    }
                }
            }
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();
        }

        if (localPackagesFoldouts.Length != localPackageRootFolders.Count)
        {
            var oldFoldoutStates = localPackagesFoldouts;
            localPackagesFoldouts = new bool[localPackageRootFolders.Count];
            for (int i = 0; i < oldFoldoutStates.Length; i++)
            {
                if (i >= localPackageRootFolders.Count)
                {
                    break;
                }
                localPackagesFoldouts[i] = oldFoldoutStates[i];
            }
        }

        {
            bool prevEnabled = GUI.enabled;
            if (selectedPackages.Count == 0)
            {
                selectingForInstall = true;
            }
            for (int p = 0; p < localPackagesFoldouts.Length; p++)
            {
                if (localPackagesFoldouts[p] = EditorGUILayout.Foldout(localPackagesFoldouts[p], localPackageRootFolders[p], true))
                {
                    for (int i = 0; i < localPackages.Count; i++)
                    {
                        if (localPackages[i].rootFolderIndex == p)
                        {
                            bool installed = false;
                            if (installedPackages != null)
                            {
                                installed = installedPackages.Contains(localPackages[i].name);
                            }
                            bool selected = selectedPackages.Contains(localPackages[i]);
                            bool dependencySelected = selectedDependencyPackages.Contains(localPackages[i]);
                            if (selectedPackages.Count > 0)
                            {
                                GUI.enabled = GUI.enabled && (installed != selectingForInstall);
                            }
                            Color prevColour = GUI.color;
                            if (dependencySelected)
                            {
                                GUI.enabled = false;
                                GUI.color = new Color(0.6f, 1.0f, 0.6f);
                            }
                            string statusMessage = "";
                            if (installed)
                            {
                                GUI.color = new Color(1.0f, 0.9f, 0.5f);
                                //if we are selecting for uninstall and one of the selected packages depends on this one
                                //highlight it in red?
                                statusMessage = "installed";
                                if (!selectingForInstall)
                                {
                                    foreach (var item in selectedPackages)
                                    {
                                        if (localPackages[i].dependencies != null && localPackages[i].dependencies.ContainsKey(item.name))
                                        {
                                            statusMessage += ", dependends on " + item.displayName;
                                            GUI.color = new Color(1.0f, 0.6f, 0.6f);
                                        }
                                    }
                                }
                            }
                            string displayName = localPackages[i].displayName;
                            if (!string.IsNullOrEmpty(statusMessage))
                            {
                                displayName += " (" + statusMessage + ")";
                            }
                            bool check = GUILayout.Toggle(selected || dependencySelected, displayName);
                            GUI.enabled = prevEnabled;
                            GUI.color = prevColour;
                            if (selected && !check)
                            {
                                selectedPackages.Remove(localPackages[i]);
                                if (installDependencies && selectingForInstall)
                                {
                                    RefreshSelectedDependencies();
                                }
                            }
                            else if (!selected && check && !dependencySelected)
                            {
                                if (selectedPackages.Count == 0)
                                {
                                    selectingForInstall = !installed;
                                }
                                selectedPackages.Add(localPackages[i]);
                                if (installDependencies && selectingForInstall)
                                {
                                    RefreshSelectedDependencies();
                                }

                            }
                        }
                    }
                }
            }
            if (installedPackages == null || installedPackages.Count == 0)
            {
                if (ListRequest != null)
                {
                    GUILayout.Label("Polling installed packages...");
                }
                else if (GUILayout.Button("Poll installed packages"))
                {
                    if (ListRequest == null)
                    {
                        ListRequest = Client.List(true);
                        EditorApplication.update += PollInstalledPackagesProgress;
                    }
                    else
                    {
                        Debug.Log("polling already in progress");
                    }
                }
            }

            GUI.enabled = GUI.enabled && selectedPackages.Count > 0 &&
                (installedPackages != null);
            if (selectingForInstall)
            {
                if (GUILayout.Button("Install selected packages"))
                {
                    Debug.Log("Install Selected");
                    addPackages = new List<string>();
                    for (int i = selectedDependencyPackages.Count - 1; i >= 0; --i)
                    {
                        addPackages.Add(selectedDependencyPackages[i].name);
                    }
                    foreach (var item in selectedPackages)
                    {
                        addPackages.Add(item.name);
                    }
                    AddNextPackageFromQueue();
                }
            }
            else
            {
                Color prevColour = GUI.color;
                GUI.color = new Color(0.9f, 0.5f, 0.6f);
                if (GUILayout.Button("Uninstall selected packages"))
                {
                    Debug.Log("Uninstall Selected");
                    removePackages = new List<string>();
                    foreach (var item in selectedPackages)
                    {
                        removePackages.Add(item.name);
                    }
                    RemoveNextPackageFromQueue();
                }
                GUI.color = prevColour;
            }
            GUI.enabled = prevEnabled;
            bool dependenciesCheck = GUILayout.Toggle(installDependencies, "Install dependencies");
            if (dependenciesCheck && !installDependencies)
            {
                installDependencies = true;
                RefreshSelectedDependencies();
            }
            else if (!dependenciesCheck && installDependencies)
            {
                selectedDependencyPackages.Clear();
                installDependencies = false;
            }

            if (GUILayout.Button("Create local package"))
            {
                string localPackageFolder = "";
                if (!string.IsNullOrEmpty(localPackageRootFolders[0]))
                {
                    localPackageFolder = localPackageRootFolders[0];
                }
                CreateLocalPackagePopup.Init(localPackageFolder);
            }

            if (localPackages == null || localPackages.Count == 0)
            {
                if (GUILayout.Button("Poll local packages"))
                {
                    localPackages = GetPackageInfoForLocalPackageFolders();
                    if (ListRequest == null)
                    {
                        ListRequest = Client.List(true);
                        EditorApplication.update += PollInstalledPackagesProgress;
                    }
                    else
                    {
                        Debug.Log("polling already in progress");
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    void RefreshSelectedDependencies()
    {
        selectedDependencyPackages.Clear();
        bool addedDependency = false;
        foreach (var package in selectedPackages)
        {
            if (package.dependencies != null)
            {
                foreach (var name in package.dependencies.Keys)
                {
                    var dependency = localPackages.Where(x => x.name == name).FirstOrDefault();
                    if (dependency != null)
                    {
                        if (!selectedDependencyPackages.Contains(dependency))
                        {
                            selectedDependencyPackages.Add(dependency);
                            addedDependency = true;
                        }
                    }
                }
            }
        }
        while (addedDependency)
        {
            addedDependency = false;
            foreach (var package in selectedDependencyPackages)
            {
                if (package.dependencies != null)
                {
                    foreach (var name in package.dependencies.Keys)
                    {
                        var dependency = localPackages.Where(x => x.name == name).FirstOrDefault();
                        if (dependency != null)
                        {
                            if (!selectedDependencyPackages.Contains(dependency))
                            {
                                selectedDependencyPackages.Add(dependency);
                                addedDependency = true;
                            }
                        }
                    }
                }
            }
        }
    }

    public static Dictionary<string, string> ParsePackageDependencies(string json)
    {
        var dependenciesLabel = "\"dependencies\"";
        if (json.IndexOf(dependenciesLabel) == -1)
        {
            // Debug.Log("no dependencies");
            return null;
        }
        else
        {
            json = json.Substring(json.IndexOf(dependenciesLabel) + dependenciesLabel.Length);
            json = json.Substring(json.IndexOf("{") + 1);
            json = json.Substring(0, json.IndexOf("}"));
            // Debug.Log(json);

            var dependencies = new Dictionary<string, string>();

            while (json.IndexOf("\"") != -1)
            {
                json = json.Substring(json.IndexOf("\"") + 1);
                string key = json.Substring(0, json.IndexOf("\""));
                json = json.Substring(key.Length + 1);

                json = json.Substring(json.IndexOf("\"") + 1);
                string value = json.Substring(0, json.IndexOf("\""));
                json = json.Substring(value.Length + 1);

                dependencies.Add(key, value);
            }
            // foreach (var item in dependencies)
            // {
            //     Debug.Log(item.Key + ", " + item.Value);
            // }
            return dependencies;
        }
    }

    List<PackageInfo> GetPackageInfoForLocalPackageFolders()
    {
        List<PackageInfo> localPackages = new List<PackageInfo>();

        if (localPackageRootFolders.Count <= 0)
        {
            Debug.Log("no local package root folders listed");
            return localPackages;
        }

        for (int folderIndex = 0; folderIndex < localPackageRootFolders.Count; folderIndex++)
        {
            var localPackagesDirectory = localPackageRootFolders[folderIndex];
            string logEntry = "Folders in local packages folder (" + localPackagesDirectory + "):";

            //capture current directory to set it back when done
            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory("/" + localPackagesDirectory);
                var folders = Directory.GetDirectories(Directory.GetCurrentDirectory());
                foreach (var folder in folders)
                {
                    logEntry = string.Join("\n", logEntry, folder);
                    //should have a package.json package manifest file
                    string packageJsonPath = folder + "/package.json";
                    if (File.Exists(packageJsonPath))
                    {
                        string json = File.ReadAllText(packageJsonPath);
                        var parsedInfo = JsonUtility.FromJson<PackageInfo>(json);
                        if (string.IsNullOrEmpty(parsedInfo.displayName))
                        {
                            parsedInfo.displayName = parsedInfo.name;
                        }
                        // JsonUtility cannot read the dependencies because they are a dictionary
                        // so we extract them separately
                        parsedInfo.dependencies = ParsePackageDependencies(json);
                        //keep folder path in this info
                        parsedInfo.folder = folder;
                        parsedInfo.rootFolderIndex = folderIndex;
                        localPackages.Add(parsedInfo);
                    }
                    else
                    {
                        //logEntry = string.Join("\n", logEntry, "not a package folder");
                    }
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
                //Debug.Log(logEntry);
            }
        }

        return localPackages;
    }

    static void PollInstalledPackagesProgress()
    {
        if (ListRequest.IsCompleted)
        {
            if (ListRequest.Status == StatusCode.Success)
            {
                installedPackages = new List<string>();
                foreach (var package in ListRequest.Result)
                {
                    installedPackages.Add(package.name);
                    //Debug.Log("Package name: " + package.name);
                }
            }
            else if (ListRequest.Status >= StatusCode.Failure)
            {
                Debug.Log(ListRequest.Error.message);
            }
            ListRequest = null;
            EditorApplication.update -= PollInstalledPackagesProgress;
            if (removePackages != null && removePackages.Count > 0)
            {
                Debug.Log("resuming package removal from queue");
                RemoveNextPackageFromQueue();
            }
            else if (addPackages != null && addPackages.Count > 0)
            {
                Debug.Log("resuming package install from queue");
                AddNextPackageFromQueue();
            }
        }
    }

    // Add a package to the Project
    static void AddNextPackageFromQueue()
    {
        Debug.Log("Adding package from queue: " + addPackages[0]);
        var packageFolder = localPackages.Where(x => x.name == addPackages[0]).Select(x => x.folder).FirstOrDefault(); ;
        AddPackageRequest = Client.Add(addPackages[0] + "@file:" + packageFolder);
        addPackages.RemoveAt(0);
        EditorApplication.update += AddPackageProgress;
    }

    // Remove a package from the Project
    static void RemoveNextPackageFromQueue()
    {
        Debug.Log("Removing package from queue: " + removePackages[0]);
        var packageFolder = localPackages.Where(x => x.name == removePackages[0]).Select(x => x.folder).FirstOrDefault(); ;
        RemovePackageRequest = Client.Remove(removePackages[0]);
        removePackages.RemoveAt(0);
        EditorApplication.update += RemovePackageProgress;
    }

    static void AddPackageProgress()
    {
        if (AddPackageRequest.IsCompleted)
        {
            if (AddPackageRequest.Status == StatusCode.Success)
                Debug.Log("Installed: " + AddPackageRequest.Result.packageId);
            else if (AddPackageRequest.Status >= StatusCode.Failure)
                Debug.Log(AddPackageRequest.Error.message);

            if (addPackages.Count > 0)
            {
                Debug.Log(addPackages.Count + " more packages in Queue");
                AddPackageRequest = null;
                // an assembly reload will happen after a package install which will wipe static values
                // so this list will be emptied and the EditorApplication.update will be empty
                // save the list in playerprefs and then resume by reading the prefs on assembly reload
                string saveValue = string.Join(",", addPackages);
                Debug.Log(saveValue);
                PlayerPrefs.SetString("AddPackagesQueue", saveValue);
            }
            else
            {
                Debug.Log("all queued packages installed");
                PlayerPrefs.DeleteKey("AddPackagesQueue");
            }
            EditorApplication.update -= AddPackageProgress;
        }
    }

    static void RemovePackageProgress()
    {
        if (RemovePackageRequest.IsCompleted)
        {
            if (RemovePackageRequest.Status == StatusCode.Success)
                Debug.Log("Removed package: " + RemovePackageRequest.PackageIdOrName);
            else if (RemovePackageRequest.Status >= StatusCode.Failure)
                Debug.Log(RemovePackageRequest.Error.message);

            if (removePackages.Count > 0)
            {
                Debug.Log(removePackages.Count + " more packages in Queue (remove)");
                RemovePackageRequest = null;
                // an assembly reload will happen after a package install which will wipe static values
                // so this list will be emptied and the EditorApplication.update will be empty
                // save the list in playerprefs and then resume by reading the prefs on assembly reload
                string saveValue = string.Join(",", removePackages);
                Debug.Log(saveValue);
                PlayerPrefs.SetString("RemovePackagesQueue", saveValue);
            }
            else
            {
                Debug.Log("all queued packages removed");
                PlayerPrefs.DeleteKey("RemovePackagesQueue");
            }
            EditorApplication.update -= RemovePackageProgress;
        }
    }
}