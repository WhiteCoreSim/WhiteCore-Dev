#!/bin/bash
# Startup script for WhiteCore-Sim grid server service
# Versions 0.9.2+
#
# July 2017
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting WhiteCore GridServer...
screen -S Grid -d -m mono WhiteCore.Server.exe -skipconfig
sleep 3
screen -list
echo "To view the Grid server console, use the command : screen -r Grid"
echo "To detach from the console use the command : ctrl+a d  ..ctrl+a > command mode,  d > detach.."
echo
