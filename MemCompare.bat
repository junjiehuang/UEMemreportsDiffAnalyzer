
set MEMREPORT_FILE1="./MemReports/l_fighting.memreport"
set MEMREPORT_FILE2="./MemReports/h_fighting.memreport"
set CSV_PATH="./MemReports/"

.\Bin\MemDiffCmd.exe -1f %MEMREPORT_FILE1% -2f %MEMREPORT_FILE2% -o %CSV_PATH%

pause