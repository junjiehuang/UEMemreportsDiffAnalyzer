
set MEMREPORT_FILE1="./MemReports/01-17.19.54.memreport"
set MEMREPORT_FILE2="./MemReports/30-18.40.46.memreport"
set CSV_PATH="./MemReports/"

.\bin\Debug\UEMemreportsDiffAnalyzer.exe -1f %MEMREPORT_FILE1% -2f %MEMREPORT_FILE2% -o %CSV_PATH%

pause