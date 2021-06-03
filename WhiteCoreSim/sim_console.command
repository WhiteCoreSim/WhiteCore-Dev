#!/bin/bash
# Start the WhiteCore-Sim Standalone server
# Version 0.9.6+
#
# June 2021 - Always run config at startup
# greythane @ gmail.com

WOASDIR="${0%/*}"
cd $WOASDIR
echo $WOASDIR

cd ./bin
echo Starting WhiteCore Standalone Simulator...
mono WhiteCore.exe
wait

exit

