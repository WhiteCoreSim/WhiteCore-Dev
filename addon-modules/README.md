These are optional modules that can be used with WhiteCore

# Installation

To install, there are two ways to install, auto-installation, or manual compilation

## Automated
1. Start WhiteCore.exe or WhiteCore.Server.exe
2. Type 'compile module <path to the build.am of the module that you want>' into the console and it will install the module for you and tell you how to use or configure it.

## Manual Compilation and installation:
Copy the selected directory to the addon-modules of the main source code directory.
Each module should be in it's own tree and the root of the tree should contain a file named "prebuild.xml", which will be included in the main prebuild file.

The prebuild.xml should only contain <Project> and associated child tags. 
The <?xml>, <Prebuild>, <Solution> and <Configuration> tags should not be included since the add-on modules prebuild.xml will be inserted directly into the main prebuild.xml


# Known Issues
