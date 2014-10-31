DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd ${DIR}
mkdir -p WhiteCoreSim/WhiteCoreDocs/doxygen
rm -fr WhiteCoreSim/WhiteCoreDocs/doxygen/*
doxygen doxygen.conf
--