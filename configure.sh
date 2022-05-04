#!/bin/bash
# Run prebuild to configure and create the appropriate Solution and Project files for building WhiteCore-Sim
#
# June 2021
# Rowan Deppeler <greythane@gmail.com>
#

echo "This will retrieve build and configure WhiteCore ready to login"
echo "This assumes that WhiteCore will be run from the home directory"
 
MODE="grid"
CONFIGURE=true
RUN=true

# install build environment
sudo apt install mono-complete
cd ~/

# clone the  WhiteCore repository 
git clone https://github.com/greythane/WhiteCore-Dev.git

# change to the code main directory for building
cd WhiteCore-Dev

# ensure that the library submodules are present and the latest
git submodule update --init --recursive

echo "Commencing build"
./autobuild.sh

# ensure correct build before proceeding
read -p "Build completed. Are any errors reported(Yes, No) [yes]: " errok
if [[ $errok == "Yes" ]]; then CONFIGURE=true; fi
if [[ $errok == "yes" ]]; then CONFIGURE=true; fi
if [[ $errok == "y" ]]; then CONFIGURE=true; fi
if [[ $CONFIGURE != true ]]; then
    echo "Exiting to correct build options"
    exit 1
fi

# Good to go
# Copy entire WhiteCoreSim directory to the home directory
cp -r WhiteCoreSim ~/WhiteCoreSim

# change to the WhiteCoreSim/bin directory for initial configuration
cd ~/WhiteCoreSim/bin

# ensure a clean start
FILE=./.whitecore.config
if test -f "$FILE"; then
    rm $FILE
fi

FILE=./.whitecoregrid.config
if test -f "$FILE"; then
    rm $FILE
fi

# Configure the WhiteCore grid server
echo "Configuring the grid server"
echo "You will be asked a series of questions regarding your server setup"
echo "If you select 'MySql' as your database, option '2' you will need"
echo "  your database address, name, user and password for access"
echo ""
echo "If using SQLite, database details are assumed"
echo ""
read "Press enter to continue..." cont

mono WhiteCore.Server.exe --reconfigure

# the grid server configuration is now complete
# Re run the server to setup required data and assets
echo "Initial run of the grid server to install required assets"
echo "On completion, please 'quit' to exit the server"
mono WhiteCore.Server.exe

# Restart the grid server in a screen session ready for configuring the region server
echo "Restarting the grid server to run in the backgroung"
cd ..
./run_gridservice.sh

echo " Configuring the region server"
cd ./bin
mono WhiteCore.exe --reconfigure

echo "Initial run of the region server to create system users and landing region"
echo "On cmpletion, please 'quit' to exit the server"
# Restart region server to complete configuration
mono WhiteCore.exe

# Restart the region server in a screen session
echo "Restarting the region server to run in the backgroung"
cd ..
./run_simservice.sh

echo "Inital configuration is complete"
echo "You can now log in using your client at 'http://<your sim address>:8002'"
echo "Note:  You will need to add your new grid to the grid listing of your client"
echo "       Enter 'http://<your sim address>:8002' and 'Apply' to setup the client"
echo ""
echo "You can check that the servers are up and running correctly by accessing"
echo " the appropriate screen session"  
echo "screen -r Grid"
echo "screen -r Sim"
echo ""
echo "The servers will run until 'killed' or a system reboot occurs"
echo "You can use the 'ps' command to check if WHiteCore is running"
echo "  ps -ax | grep WhiteCore"
echo ""
echo "If no instances of WhiteCore are running"
echo "  start the grid and region servers with the command"
echo " ./run_gridmode.sh"
echo ""
echo "Greythane - June 2021"

