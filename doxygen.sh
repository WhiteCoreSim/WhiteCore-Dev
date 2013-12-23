DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd ${DIR}
mkdir -p WhiteCoreDocs/doxygen
rm -fr WhiteCoreDocs/doxygen/*
doxygen doxygen.conf