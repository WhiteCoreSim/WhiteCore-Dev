#!/bin/bash
# Startup script for the WhiteCore-Sim Region server service
# Versions 0.9.2+
#
# July 2017
# greythane @ gmail.com
#

cd ./bin
wait
echo Starting WhiteCore Region Simulator...
screen -S Sim -d -m mono WhiteCore.exe -skipconfig
sleep 3
screen -list
echo "To view the Sim server console,  use the command : screen -r Sim"
echo "To detach from the console use the command : ctrl+a d  ..ctrl+a > command mode,  d > detach.."
echo
