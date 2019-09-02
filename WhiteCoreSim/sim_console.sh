#!/bin/bash
# Start the WhiteCore-Sim server only
# Versions 0.9.5+
#
# August 2019 - Always run config at startup
# greythane @ gmail.com

cd ./bin
sleep 1
echo Starting Standalone Region Simulator...
mono WhiteCore.exe
wait
exit

