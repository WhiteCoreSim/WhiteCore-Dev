#!/bin/bash
# startup script for WhiteCore-Sim standalone
# Versions 0.9.5+
#
# August 2019 - Always run config at startup - will pass through after initial setup
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting Standalone Region Simulator...
screen -S Sim -d -m mono WhiteCore.exe
sleep 3
screen -list
echo "To view the Sim console, use the command : screen -r Sims"
echo "To detach from the console use the command : ctrl+a d  ...ctrl+a [command mode],  d [detach]"
echo


