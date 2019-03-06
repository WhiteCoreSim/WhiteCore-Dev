# WhiteCore Add-in Modules

Optional modules to be used with WhiteCore should be placed in this directory prior to building.  
These modules add additional functionality to your WhiteCore simulator installation.

## Installation

You will need to copy the required module from the [WhiteCore-Optional-Modules](https://github.com/WhiteCoreSim/WhiteCore-Optional-Modules)  repository.  The code for each module is contained in a subdirectory, which should be copied in it's entirety to this 'addon-modules' directory.


There are two ways to build and install these modules, including the module compilation when building WhiteCore or installation from within WhiteCore.

### Manual Compilation and installation:
Copy the selected module directory to the 'addon-modules' directory of the main source code directory.
Each module should be in it's own subdirectory with the root of the subdirectory containing a file named "prebuild.xml".  
***This is the standard layout of the Optional Modules repository.***

Executing the 'runprebuild' script file for your operating system will include the module and build it with the main WhiteCore codebase.


### From within Whitecore
1. Start WhiteCore.exe or WhiteCore.Server.exe
2. Type 'compile module <path to the build.am of the module that you want>' into the console and it will install the module for you and tell you how to use or configure it.


*The prebuild.xml should only contain <Project> any associated child tags.  
The `<?xml>, <Prebuild>, <Solution> and <Configuration>` tags should not be included since the add-on modules prebuild.xml will be inserted directly into the main prebuild.xml*


## Notes
- Latest module update of March 4th, 2019
- All modules available compile correctly.

### FreeswitchVoice:
  Has not been tested fully so there may be issues. Testers would be appreciated.
### IrcChat:
  The 'group' chat is still under development and may work in unexpected ways.  The Region chat has been tested without issue.
### IARModifierGUI:
  Is a standalone program, still largely untested.  Have a good backup if you are modifying the DefaultInventory.iar


## Support
Support is available from various sources.

* IRC channel #whitecore-support on freenode.  Use your favourite IRC client or the simple web interface available at
 <http://webchat.freenode.net?channels=%23whitecore-support>
 
 *The IRC channel is monitored continuously by the developers but there may not be someone to answer you question immediately (different timezones), but it will be answered if you are patient.*

* Check out <https://whitecore-sim.org> for the latest developments, downloads and forum

* If you find a problem please log an issue on the repository issue tracker [WhiteCore-Optional-Modules issues](https://github.com/WhiteCoreSim/WhiteCore-Optional-Modules/issues)

*Greythane - March 2019*
