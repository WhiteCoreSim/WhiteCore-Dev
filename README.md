# WhiteCore Sim

The WhiteCore Development Team has moved the original Aurora virtual world server, derived from the OpenSim project, to a new level.

The structure and code base has been heavily revised and is under continual development.

There is a heavy emphasis on supporting all users, increased technology focus and working with other developers, whether it be viewer based developers or server based developers, to develop a set of features that is stable, fast with simplified usability for users.

## Build Status

Windows .Net 4.6 [![Build status](https://ci.appveyor.com/api/projects/status/tj3pr2xb4rg6ospe/branch/master?svg=true)](https://ci.appveyor.com/project/fly-man-/whitecore-dev/branch/master)

Linux 64 Bit [![Build Status](https://travis-ci.org/WhiteCoreSim/WhiteCore-Dev.svg?branch=master)](https://travis-ci.org/WhiteCoreSim/WhiteCore-Dev)

*NOTES:*

*- As of January 2017, the LibOMV libraries are included as a submodule of the WhiteCore repo. When cloning, ensure that the submodules are included.*

`git clone --recursive https://github.com/WhiteCoreSim/WhiteCore-Dev.git`

To update an existing repository that does not have the LibOMV submodule

	cd <your WhiteCore repository>
	git submodule init
	git submodule update

*If you do not know what submodules are, or you are not using git from the command line, PLEASE make sure to fetch the submodules too.*

**If you download the repo using the zip file option, you will also need to download the Whitecore-LibOMV submodule and extract it in your local WhiteCore repo.**
`https://github.com/WhiteCoreSim/Whitecore-LibOMV`

*- For those using the master, please report back when you are having issues with the builds. We, the developers, can't test everything and we hope that you, the users, are able to help us report things that break. Please use the Issue Tracker with the predefined text to make it easier to report issues*

*- The BulletSim physics engine has been updated recently and is an alternative to the Open Dynamics Engine that is used as default. Please try it out and tell us if it's working (or not) properly*

*- For Linux or OSX users, it's advised that the latest Mono version available be installed to avoid possible problems.*

More information can be found here:  
<http://www.mono-project.com/docs/getting-started/install/linux/>

## Support
Support is available from various sources.

* IRC channel #whitecore-support on freenode  
 <http://webchat.freenode.net?channels=%23whitecore-support>
 
 *The IRC channel is monitored continuously by the developers but there may not be someone to answer you question immediately (different timezones), but it will be answered if you are patient.*

* Check out <http://whitecore-sim.org> for the latest developments, downloads and forum

* Google + community for WhiteCore with a friendly bunch that is happy to answer questions. Find it at <https://plus.google.com/communities/113034607546142208907?cfem=1>

## Configuration
WhiteCore is configured to run 'Out of the Box'.
The default configuration is for 'Standalone' mode, uses the embedded SQLite database and is intended for single user testing or development.  
For Grid operation or specific tailoring to your requirements, check the documentation.

*To see how to configure WhiteCore, look at "Setting up WhiteCore.txt" in the WhiteCoreDocs folder for more information*

#####Windows:
   Run the 'runprebuild.bat' file.
   This will check you current system configuration, compile the correct Visual Studio soultion and project files and prompt you to build immediately (if desired)
   *[ A specific 'runprebuild vs2015.bat' is provided if you use Visual Studio 2015+ ]* 

#####*nix: (Also OSX)
   Execute the 'runprebuild.sh' from a terminal or console shell.
   You will be prompted for your desired configuration, the appropriate solution and project files for Mono will be compiled and finally, prompt you to build immediately (if desired)

Alternatively, execute the 'autobuild.sh' script to configure and build WhiteCore to your system specifications.
   
#####OSX: (Finder)
   Run the 'runprebuild.command' shell command by 'double clicking' in Finder.
   You will be prompted for your desired configuration, the appropriate solution and project files for Mono will be compiled and finally, prompt you to build immediately (if desired)
   	   
## Compiling WhiteCore
*To compile WhiteCore, look at the Compiling.txt in the WhiteCoreDocs folder for more information*

*NOTE:
  For Windows 7, 8 and 10, when compiling, you may see some warnings indicating that the core library does not match what is specified.
  This is an issue with how Microsoft provides the Net 4.5 packages and can be safely ignored as Windows will actually use the correct library when WhiteCore is run *
  
## Router issues
If you are having issues logging into your simulator, take a look at <http://forums.osgrid.org/viewtopic.php?f=14&t=2082> in the Router Configuration section for more information on ways to resolve this issue.

### Older Versions
*NOTE: 
 As of Version 0.9.2, the WhiteCore repository format has changed.  
 The WhiteCore-Optional-Modules repository has also been updated for the new structure.
 To ensure correct compiling, use the latest commits of the WhiteCore-Dev or a release version >= 0.9.2*

*Please see the "Updating from a pre 0.9.2 version.txt" file for details on files and configurations that will need to be modified
 The document can be found in the WhiteCoreDocs directory*
