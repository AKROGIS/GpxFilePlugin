@echo off
rem %cd% does not work when "run as administrator" on win7 
rem set dll=%cd%\GpxPlugin.dll
set dll=%0%
set dll=%dll:unreg.bat=GpxPlugin.dll%

if defined CommonProgramFiles(x86) goto x64
	rem not 64bit OS, assume 32bit
	set regasm="%CommonProgramFiles%\ArcGIS\bin\ESRIRegAsm.exe"
	goto check1
:x64
	set regasm="%CommonProgramFiles(x86)%\ArcGIS\bin\ESRIRegAsm.exe"
:check1
	if exist %regasm% goto check2
	echo Error: %regasm% not found.
	pause
	goto end
:check2
	if exist %dll% goto doit
	echo Error: %dll% not found.
	pause
	goto end
:doit
	%regasm% %dll% /u /p:Desktop
:end
