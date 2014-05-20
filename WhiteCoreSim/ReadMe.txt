WhiteCore-Dev 0.9.2+ (git)
Rowan Deppeler <greythane@gmail.com>
May 2014
===============================================

Updating
=========
Checkout the 'Build Your Own.txt' file for details.

Delete the existing 'WhiteCoreSim/bin' subdirectory.
Re-compile and copy/paste the new 'WhiteCoreSim/bin' subdirectory from your build environment.
re-start..


** The scripts provided **
==========================

 *nix flavours (including Mac)
-------------------------------
All these scripts are intended to be executed form a terminal window.

Note:
 These 'background' modes require the 'screen' program installed on your system.
 Linux (ubuntu variations) >> apt-get install screen;  Mac >> already available
  
run_standalonemode.sh       	: Run WhiteCore standalone mode in background
run_gridmode.sh             	: Run WhiteCore grid mode in background

The following are for testing, maintenance etc...
sim_console.sh       			: Start only the WhiteCore standalone server
grid_console.sh      			: Start only the WhiteCore grid server

The same commands are available as gui scripts

sim_console.command				: Mac
sim_console.bat					: Windows
grid_console.command			: Mac
grid_console.bat				: Windows


Grid mode
=========
This configuration has been setup to run as a standalone simulator. If you wish to re-configure
and use the Grid mode of operation, change the selected include mode in the 'Main.ini' file.

Edit Main.ini file in
Config > Sim > Main.ini

Comment the "Include-Standalone =" line.
Uncomment the "Include-Grid =" line.

Save and re-start.


Questions?
==========
Checkout the #whitecore-support irc channel on freenode,
or check into the Google+ AuroraSim/WhiteCoreSim group at 
https://plus.google.com/communitites/113034607546142208907

Rowan Deppeler
<greythane @ gmail.com>

May 2014
=======================

For licensing information, please see the relevant licenses.
