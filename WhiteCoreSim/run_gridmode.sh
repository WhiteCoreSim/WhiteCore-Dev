#!/bin/bash
# Startup script for WhiteCore-Sim in full Grid mode
# Versions 0.9.6+
#
# June 2021 - Always run config at startup - will pass through after initial setup
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting WhiteCore GridServer...
screen -S Grid -d -m mono WhiteCore.Server.exe
sleep 3
echo Starting WhiteCore Region Simulator...
screen -S Sim -d -m mono WhiteCore.exe
sleep 3
screen -list
echo "To view the Grid server console, use the command : screen -r Grid"
echo "To view the Sim server console,  use the command : screen -r Sim"
echo "To detach from the console use the command : ctrl+a d  ..ctrl+a > command mode,  d > detach.."
echo
