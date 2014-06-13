#!/bin/bash
# Start the WhiteCore-Sim Standalone server
# Version 0.9.2+
#
# May 2014
# greythane @ gmail.com

WOASDIR="${0%/*}"
cd $WOASDIR
echo $WOASDIR

cd ./bin
echo Starting WhiteCore Standalone Simulator...
mono WhiteCore.exe -skipconfig
wait

exit

