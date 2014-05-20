#!/bin/bash

mono ./Prebuild.exe /target vs2010 /targetframework v4_0 /conditionals "LINUX;NET_4_0"

if [ -d ".git" ]; then git log --pretty=format:"WhiteCore (%cd.%h)" --date=short -n 1 > WhiteCoreSim/bin/.version; fi

unset makebuild
unset makedist

while [ "$1" != "" ]; do
    case $1 in
	build )       makebuild=yes
                      ;;
	dist )        makedist=yes
                      ;;
    esac
    shift
done

if [ "$makebuild" = "yes" ]; then
    xbuild WhiteCore.sln
    res=$?

    if [ "$res" != "0" ]; then
	exit $res
    fi

    if [ "$makedist" = "yes" ]; then
	rm -f WhiteCore-autobuild.tar.bz2
	tar cjf WhiteCore-autobuild.tar.bz2 WhiteCoreSim/bin
    fi
fi
