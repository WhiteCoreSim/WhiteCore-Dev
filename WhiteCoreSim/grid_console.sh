#!/bin/bash
# Start WhiteCore-Sim grid server only
# Versions 0.9.6+
#
# June 2021 - Always run config at startup
# greythane @ gmail.com

cd ./bin
wait
echo Starting WhiteCore Grid Server...
mono WhiteCore.Server.exe

