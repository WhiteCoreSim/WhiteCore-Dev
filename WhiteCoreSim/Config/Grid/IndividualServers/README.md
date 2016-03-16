# Individual Server Configuration
=================================

In this folder are example 'ini' files to run individual servers for each of the required services.

Note:  These files are NOT INCLUDED in the standard grid configuration.
To use your configurations you will need to modify the '[Handlers]' 
section of 'WhiteCoreServer.ini'

Example: To configure for a seperate Asset Server the Configuration for
the 'WhiteCore.Server.exe' instance could be changed to...

;; If you wish to run separate *.server.exe files, you will need
;; to comment this line and configure these services externally.
;Include-Single = Grid/ServerConfiguration/SingleServerInstance.ini
Include-Simgle = Grid/IndividualServers/WhiteCore.AssetServer.ini

