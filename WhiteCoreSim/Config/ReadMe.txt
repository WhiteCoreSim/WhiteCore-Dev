WhiteCore-Dev 0.9.5+ (git)
Rowan Deppeler <greythane@gmail.com>
August 2019
===============================================

** The default configuration **
===============================
The default configuration of WhiteCore is setup to run in Standalone mode and
  to use an SQLite database, with no pre-configured users or regions.
On initial startup, you will be asked to create your initial region, 
  together with your first user.

** Quick Customising **
=======================
You can quickly set a few options to customise your WhiteCore installation.
To do this, modify the commented out settings in 'MyWorld.ini' located in the
  'Config' folder.  This will allow you to set the name of your Sim, configure your default
   region and specify an IP address to use if you do not want to use your external IP.

** Grid mode **
===============
If you wish to re-configure and use the Grid mode of operation, 
  change the selected include mode at the top of the 'WhiteCore.ini' file.

Edit WhiteCore.ini file in the 'Config' directory

At the top of the file....
Comment the "Include-Standalone =" line.
Uncomment the "Include-Grid =" line.


**    Configuration files     **
================================
This Folder contains the configuration files necessary to setup
  your WhiteCor-Sim system.
The files and folders that are used for the various modes of operations
  are as follows...

 WhiteCore region server
-------------------------------
The main configuration is in the 'WhiteCore.ini' file.
The file 'MyWorld.ini' is a 'quick config', override.  The settings contained
  within will override the same settings elsewhere in the Standalone/*.ini or Sim/*.ini files.

 WhiteCore grid server
-------------------------------
The main configuration is in the 'WhiteCore.server.ini' file.
The file 'MyGrid.ini' is a 'quick config', override.  The settings contained
  within will override the same settings elsewhere in the Grid/*.ini files.

 Sim settings
-------------------------------
The folder Sim/* contains the settings for the WhiteCore region simulator.
These files configure how the region will perform and what will be available in region.

 Standalone Mode
-------------------------------
The folder 'Standalone' contains the startup settings for a standalone sim.
These settings will be used when the WhiteCore sim uses a combined grid 
  and region in a single instance.

 GridRegion Mode
-------------------------------
The folder 'GridRegion' contains the startup settings for When the WhiteCore sim 
  region will be connected to an external (or local) Grid.

 Grid settings
-------------------------------
The folder Grid/* contains the settings for the WhiteCore grid server.
These files configure how the WhiteCore Grid Server will perform and what 
  will be available when a region is connected to the grid.


Questions?
==========
Checkout the #whitecore-support irc channel on freenode,
or check into the MeWe community for WhiteCore 
  https://mewe.com/group/5cb284545da1780ba88ca30d 
  where a friendly group is happy to answer questions.

Rowan Deppeler
<greythane @ gmail.com>

August 2019
=======================

For licensing information, please see the relevant licenses.
