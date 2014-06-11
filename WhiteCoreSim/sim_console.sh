#!/bin/bash
# Start the WhiteCore-Sim server only
# Versions 0.9.2+
#
# May 2014
# greythane @ gmail.com

cd ./bin
sleep 1
echo Starting Standalone Region Simulator...
mono WhiteCore.exe -skipconfig
wait
exit

