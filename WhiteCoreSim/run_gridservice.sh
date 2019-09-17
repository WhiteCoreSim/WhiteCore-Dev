#!/bin/bash
# Startup script for WhiteCore-Sim grid server service
# Versions 0.9.5+
#
# August 2019 - Always run config at startup - will pass through after initial setup
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting WhiteCore GridServer...
screen -S Grid -d -m mono WhiteCore.Server.exe
sleep 3
screen -list
echo "To view the Grid server console, use the command : screen -r Grid"
echo "To detach from the console use the command : ctrl+a d  ..ctrl+a > command mode,  d > detach.."
echo
