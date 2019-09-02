#!/bin/bash
# Start the WhiteCore-Sim Grid server
# Version 0.9.5+
#
# August 2019 - Always run config at startup
# greythane @ gmail.com

WOASDIR="${0%/*}"
cd $WOASDIR
echo $WOASDIR

cd ./bin
echo Starting WhiteCore Grid server...
mono WhiteCore.Server.exe
wait

exit

