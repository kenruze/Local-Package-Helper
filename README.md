# Local-Package-Helper
A Unity tool window to show and install available local packages and for creating new local packages.

## Installation

##### Unity 2019.3 or later:
Use the Unity Package Manager to add a new package from a Github URL. Use the URL of this project:

    https://github.com/kenruze/Local-Package-Helper

##### Unity 2017.2 or later:
Edit _package.json_ in the "Packages" folder of your Unity project. Add an entry into the _dependencies_ section using the package name from the _package.json_ file in this repo, and the URL of this project:

    "com.moletotem.localpackagehelper": "https://github.com/kenruze/Local-Package-Helper.git",

## Operation:

Open the Local Package Helper tool window under the Window menu in Unity

Either use the window to add the directory for local package folders, or add a directory to _localPackageFolders.txt_ in this package folder and re-open the Local Package Helper window

The window shows available local packages and indicates if they are installed. 

Select packages and press the "install selected packages" button to install them. Available local packages listed in the dependencies will also be selected by default.

Select installed packages to remove them. The "install selected packages" button is replaced by a red, "Uninstall selected packages" button.

Press the "Create local package" to create a new local package. Pressing the button opens a window with configuration options and "Create Package" and "Cancel" buttons.
