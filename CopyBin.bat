rmdir /s/q Bin
mkdir Bin

xcopy /E/H/C/I MemDiffCmd\bin\debug\net6.0\ Bin\
xcopy /E/H/C/I MemDiffApp\bin\debug\net6.0-windows\ Bin\

pause