#!/bin/bash
# Start the WhiteCore-Sim Grid server
# Version 0.9.2+
#
# May 2014
# greythane @ gmail.com

WOASDIR="${0%/*}"
cd $WOASDIR
echo $WOASDIR

cd ./bin
echo Starting WhiteCore Grid server...
mono WhiteCore.Server.exe -skipconfig
wait

exit

