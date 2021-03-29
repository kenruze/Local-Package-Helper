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
    List<bool> localPackagesFoldouts = new List<bool>();
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
                    Debug.Log("chose " + options[i]);
                    string path = options[i];
                    EditorUtility.RevealInFinder(path);
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

    void SaveLocalPackagesFile(SerializedObject localPackagesHelperSerializedObject)
    {
        // save folders
        localPackagesHelperSerializedObject.ApplyModifiedProperties();
        // get local package folders from text file
        var assetPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("localPackageFolders").FirstOrDefault());
        if (File.Exists(assetPath))
        {
            // Debug.Log(assetPath);
            File.WriteAllLines(assetPath, localPackageRootFolders);
        }
        else
        {
            Debug.Log("no file \"localPackageFolders\" found; could not save local package folders");
            //just save the file somewhere
        }
        UpdateFoldouts();
    }

    void UpdateFoldouts()
    {
        if (localPackagesFoldouts.Count != localPackageRootFolders.Count)
        {
            int c = 0;
            for (; c < localPackageRootFolders.Count; c++)
            {
                if (localPackagesFoldouts.Count <= c)
                {
                    localPackagesFoldouts.Add(true);
                }
            }
            for (; c < localPackagesFoldouts.Count; c++)
            {
                localPackagesFoldouts.RemoveAt(c);
                --c;
            }
        }
    }

    void OnGUI()
    {
        SerializedObject serializedObject = new SerializedObject(this);
        var localPackageRootFoldersProp = serializedObject.FindProperty(nameof(localPackageRootFolders));

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add a Local Package Root Folder"))
        {
            string folderName = EditorUtility.OpenFolderPanel("Select Local Package Folder", "", "");
            if (string.IsNullOrEmpty(folderName))
            {
                // cancelled selecting new root folder
            }
            else
            {
                Debug.Log("selected folder: " + folderName);
                // add new folder
                localPackageRootFoldersProp.InsertArrayElementAtIndex(localPackageRootFoldersProp.arraySize);
                localPackageRootFoldersProp.GetArrayElementAtIndex(localPackageRootFoldersProp.arraySize - 1).stringValue = folderName;
                SaveLocalPackagesFile(serializedObject);
            }
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
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
        GUILayout.EndHorizontal();

        UpdateFoldouts();

        {
            bool prevEnabled = GUI.enabled;
            if (selectedPackages.Count == 0)
            {
                selectingForInstall = true;
            }
            if (localPackages != null && ListRequest == null)
            {
                for (int p = 0; p < localPackagesFoldouts.Count; p++)
                {
                    bool removedFolder = false;
                    GUILayout.BeginHorizontal();
                    localPackagesFoldouts[p] = EditorGUILayout.Foldout(localPackagesFoldouts[p], localPackageRootFolders[p], true);
                    string[] rootFolderDropdownOptions = new string[] { "Open Folder", "Remove Folder" };
                    int chosenRootFolderDropdownIndex = -1;
                    chosenRootFolderDropdownIndex = EditorGUILayout.Popup(new GUIContent(""), chosenRootFolderDropdownIndex, rootFolderDropdownOptions, GUILayout.Width(20));
                    if (chosenRootFolderDropdownIndex != -1)
                    {
                        // Debug.Log("chose " + rootFolderDropdownOptions[chosenRootFolderDropdownIndex]);
                        if (chosenRootFolderDropdownIndex == 0) // open folder
                        {
                            EditorUtility.RevealInFinder(localPackageRootFolders[p]);
                        }
                        else if (chosenRootFolderDropdownIndex == 1) // remove folder
                        {
                            localPackageRootFoldersProp.DeleteArrayElementAtIndex(p);
                            removedFolder = true;
                            // save folders
                            SaveLocalPackagesFile(serializedObject);
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (!removedFolder && localPackagesFoldouts[p])
                    {
                        var localPackagesDirectory = localPackageRootFolders[p];
                        localPackagesDirectory = "/" + localPackagesDirectory;
                        if (Directory.Exists(localPackagesDirectory))
                        {
                            int packagesInFolder = 0;
                            for (int i = 0; i < localPackages.Count; i++)
                            {
                                if (localPackages[i].rootFolderIndex == p)
                                {
                                    packagesInFolder++;
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
                            if (packagesInFolder == 0)
                            {
                                GUILayout.Label("[no packages in folder]");
                            }
                        }
                        else
                        {
                            GUILayout.Label("[Folder does not exist]");
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
                        localPackages = GetPackageInfoForLocalPackageFolders();
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
                        if (!installedPackages.Contains(selectedDependencyPackages[i].name))
                        {
                            addPackages.Add(selectedDependencyPackages[i].name);
                        }
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
                var addDependencySuggestions = new List<CreateLocalPackagePopup.LocalDependencySuggestion>();
                for (int i = 0; i < localPackages.Count; i++)
                {
                    addDependencySuggestions.Add(new CreateLocalPackagePopup.LocalDependencySuggestion()
                    {
                        packageName = localPackages[i].name,
                        version = localPackages[i].version,
                        localPackageRootFolderIndex = localPackages[i].rootFolderIndex,
                    });
                }
                CreateLocalPackagePopup.Init(localPackageFolder, localPackageRootFolders.ToArray(), addDependencySuggestions.ToArray());
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
            for (int i = 0; i < selectedDependencyPackages.Count; i++)
            {
                if (selectedDependencyPackages[i].dependencies != null)
                {
                    foreach (var name in selectedDependencyPackages[i].dependencies.Keys)
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
                localPackagesDirectory = "/" + localPackagesDirectory;
                if (Directory.Exists(localPackagesDirectory))
                {
                    Directory.SetCurrentDirectory(localPackagesDirectory);
                    var folders = Directory.GetDirectories(Directory.GetCurrentDirectory());
                    foreach (var folder in folders)
                    {
                        logEntry = string.Join("\n", logEntry, folder);
                        // should have a package.json file
                        string packageJsonPath = folder + "/package.json";
                        if (File.Exists(packageJsonPath))
                        {
                            string json = File.ReadAllText(packageJsonPath);
                            var parsedInfo = JsonUtility.FromJson<PackageInfo>(json);
                            if (parsedInfo == null)
                            {
                                Debug.Log("could not parse package.json in " + folder);
                                // an empty package.json folder, for example, could cause parsing to fail
                                // in this case, a package folder is skipped and does not appear in the list
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(parsedInfo.displayName))
                                {
                                    parsedInfo.displayName = parsedInfo.name;
                                }
                                // JsonUtility cannot read the dependencies because they are a dictionary
                                // so we extract them separately
                                parsedInfo.dependencies = ParsePackageDependencies(json);
                                // keep folder path in this info
                                parsedInfo.folder = folder;
                                parsedInfo.rootFolderIndex = folderIndex;
                                localPackages.Add(parsedInfo);
                            }
                        }
                        else
                        {
                            //logEntry = string.Join("\n", logEntry, "not a package folder");
                            // sub-folders that do not contain a package.json file will not appear in the package list
                            // if there are no package sub-folders, a "no packages in folder" message will be shown for that directory
                        }
                    }
                }
                else
                {
                    // folder does not exist, but will remain as an empty foldout
                    // a folder does not exist label will be added in the package list
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

    // refresh the list of packages after creating a new package
    // if install after creating was checked, also install the new package
    // packages to install are passed as a list
    public static void CreatedNewPackage(string[] installPackages)
    {
        var localPackageHelper = GetWindow<LocalPackageHelper>();
        if (installPackages != null && installPackages.Length > 0)
        {
            if (addPackages != null)
            {
                Debug.Log("already had packages queued to install, may need to install new package manually");
            }
            else
            {
                addPackages = new List<string>();
                foreach (var item in installPackages)
                {
                    Debug.Log("queue new package: " + item);
                    addPackages.Add(item);
                }
            }
        }
        else
        {
            Debug.Log("refresh package list after creating a new package");
        }
        localPackages = localPackageHelper.GetPackageInfoForLocalPackageFolders();
        if (ListRequest == null)
        {
            ListRequest = Client.List(true);
            EditorApplication.update += PollInstalledPackagesProgress;
        }
        else
        {
            Debug.Log("polling already in progress, may need to install manually");
        }
        // when refreshing local packages completes, the queued packages will be installed
        // AddNextPackageFromQueue();
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