# WhiteCore Sim

[![Join the chat at https://gitter.im/WhiteCoreSim/WhiteCore-Dev](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/WhiteCoreSim/WhiteCore-Dev?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

*NOTE:
 For those using the master, please report back when you are having issues with the builds. We, the developers, can't test everything and we hope that you, the users, are able to help us report things that break. 
 
 Please use the Issue Tracker with the predefined text to make it easier to report issues.

*NOTE:
 As of today, we have included BulletSim into the repository as a second Physics Engine. Please try it out and tell us if it's working properly*

*NOTE:
 As of Version 0.9.3, it's advised to Linux users to use a Mono version higher then 3.2.8, following a report about GC.Collect() not cleaning up memory correctly. The most current version of Mono is 4.0.1 (Released 28th April 2015)*

 More information can be found here: http://www.mono-project.com/docs/getting-started/install/linux/

The WhiteCore Development Team has begun the continuation of Aurora virtual world server.

The WhiteCore server is an OpenSim/Aurora-Sim derived project with heavy emphasis on supporting all users, 
increased technology focus, heavy emphasis on working with other developers,
whether it be viewer based developers or server based developers, 
and a set of features around stability and simplified usability for users.

## Build Status

Windows .Net 4.5 [![Build status](https://ci.appveyor.com/api/projects/status/tj3pr2xb4rg6ospe/branch/master?svg=true)](https://ci.appveyor.com/project/fly-man-/whitecore-dev/branch/master)

Linux 64 Bit [![Build Status](https://travis-ci.org/WhiteCoreSim/WhiteCore-Dev.svg?branch=master)](https://travis-ci.org/WhiteCoreSim/WhiteCore-Dev)

Pull requests [![Issue Stats](http://www.issuestats.com/github/WhiteCoreSim/WhiteCore-Dev/badge/pr)](http://www.issuestats.com/github/WhiteCoreSim/WhiteCore-Dev)

Issues closed [![Issue Stats](http://www.issuestats.com/github/WhiteCoreSim/WhiteCore-Dev/badge/issue)](http://www.issuestats.com/github/WhiteCoreSim/WhiteCore-Dev)

## Configuration
*To see how to configure WhiteCore, look at "Setting up WhiteCore.txt" in the WhiteCoreDocs folder for more information*

Windows:
   Run the 'runprebuild.bat' file.
   This will check you current system configuration, compile the correct Visual Studio 2010 soultion and project files and prompt you to build immediately (if desired)

*nix: (Also OSX)
   Execute the 'runprebuild.sh' form a terminal or console shell.
   You will be prompted for your desired configuration, the appropriate solution and project files for Mono will be compiled and finally, prompt you to build immediately (if desired)
   
OSX:
   Run the 'runprebuild.command' shell command by 'double clicking' in Finder.
   You will be prompted for your desired configuration, the appropriate solution and project files for Mono will be compiled and finally, prompt you to build immediately (if desired)
   	   
## Compiling WhiteCore
*To compile WhiteCore, look at the Compiling.txt in the WhiteCoreDocs folder for more information*

*NOTE:
  For Windows 7 and 8, when compiling, you may see some warnings indicating that the core library does not match what is specified.
  This is an issue with how Microsoft provides the Net 4.5 packages and can be safely ignored as Windows will actually use the correct library when WhiteCore is run *
  
## Router issues
If you are having issues logging into your simulator, take a look at http://forums.osgrid.org/viewtopic.php?f=14&t=2082 in the Router Configuration section for more information on ways to resolve this issue.

## Support
Support is available from various sources.

* IRC channel #whitecore-support on freenode (http://webchat.freenode.net?channels=%23whitecore-support)

* Check out http://whitecore-sim.org for the latest developments, downloads and forum

* Google + community for WhiteCore with a friendly bunch that is happy to answer questions. Find it at https://plus.google.com/communities/113034607546142208907?cfem=1

*NOTE: 
 As of Version 0.9.2, the WhiteCore repository format has changed.  
 The WhiteCore-Optional-Modules repository has also been updated for the new structure.
 To ensure correct compiling, use the latest commits of the WhiteCore-Dev or a release version >= 0.9.2*

*Please see the "Migration to 0.9.2.txt" file for details on files and configurations that will need to be modified*
