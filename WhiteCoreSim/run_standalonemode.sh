#!/bin/bash
# startup script for WhiteCore-Sim standalone
# Versions 0.9.2+
#
# May 2014
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting Standalone Region Simulator...
screen -S Sim -d -m mono WhiteCore.exe -skipconfig
sleep 3
screen -list
echo "To view the Sim console, use the command : screen -r Sims"
echo "To detach fron the console use the command : ctrl+a d  ...ctrl+a [command mode],  d [detach]"
echo


