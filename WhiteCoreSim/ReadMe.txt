WhiteCore-Dev 0.9.5+ (git)
Rowan Deppeler <greythane@gmail.com>
Fly-Man-
July 2019
===============================================

** Simplified startup scripts **
================================
Easy to use startup scripts are provided to simplify running WhiteCore.
Choose the appropriate one(s) for your system and configuration.

 *nix flavours (including Mac)
-------------------------------
All these scripts are intended to be executed from a terminal window.

The following are the general startup scripts for use in testing, maintenance etc...
sim_console.sh       			: Start only the WhiteCore standalone server
grid_console.sh      			: Start only the WhiteCore grid server


Note:
 These 'background' modes require the 'screen' program installed on your system.
   Linux (ubuntu variations) >> apt-get install screen;
   Mac >> already available
  
run_standalonemode.sh       	: Run WhiteCore standalone mode in background
run_gridmode.sh             	: Run WhiteCore grid mode in background


GUI scripts for Windows and Mac

-------------------------------

The same commands are available as gui scripts, <double_click> to execute

sim_console.command				: Mac
sim_console.bat					: Windows
grid_console.command			: Mac
grid_console.bat				: Windows


** The default configuration **
===============================
The default configuration of WhiteCore is setup to run in Standalone mode and
to use an SQLite database, with no pre-configured users or regions.
On initial startup, you will be asked to create your initial region, together with 
your first user.

** Quick Customising **
=======================
You can quickly set a few options to customise your WhiteCore installation.
To do this, modify the commented out settings in 'MyWorld.ini' located in the
 'Config' folder.  This will allow you to set the name of your Sim, configure your default
  region and specify an address to use if you do not want to use your external IP.


** Grid mode **
===============
This configuration has been setup to run as a standalone simulator. If you wish to re-configure
and use the Grid mode of operation, change the selected include mode at the top of the
'WhiteCore.ini' file.

Edit WhiteCore.ini file in the 'Config' directory

At the top of the file....
Comment the "Include-Standalone =" line.
Uncomment the "Include-Grid =" line.

Save and re-start.
Note:  You will need to use both the 'grid' and 'sim' startup scripts.

** Updating **
==============
Checkout the 'Build Your Own.txt' file in the 'WhiteCoreDocs' folder for details
 if you want to build from source.
Re-compile and copy/paste the new 'WhiteCoreSim/bin' subdirectory from your build environment.

Monthly 'Development' build snapshots will be available at the following link:
https://drive.google.com/open?id=0BxMBE_reBR03QkZDSGI4NVVjcEk

Download your desired update snapshot.
Delete or backup the existing 'WhiteCoreSim/bin' subdirectory.
Extract the update package and copy the resulting 'bin' folder to your 'WhiteCoreSim' folder.
re-start..

Questions?
==========
Checkout the #whitecore-support irc channel on freenode,
or check into the MeWe community for WhiteCore https://mewe.com/group/5cb284545da1780ba88ca30d where a friendly group is happy to answer questions.

Rowan Deppeler
<greythane @ gmail.com>

July 2019
=======================

For licensing information, please see the relevant licenses.
