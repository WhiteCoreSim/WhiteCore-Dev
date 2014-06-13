#!/bin/bash
# Start WhiteCore-Sim grid server only
# Versions 0.9.2+
#
# May 2014
# greythane @ gmail.com

cd ./bin
wait
echo Starting WhiteCore Grid Serer...
mono WhiteCore.Server.exe -skipconfig

