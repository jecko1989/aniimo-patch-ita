@echo off
rem Compila AniimoPatchITA.exe (serve solo .NET Framework 4, gia' presente in Windows 10/11).
rem Se accanto a questo file c'e' banner.jpg (immagine del gioco) viene incorporata nell'exe.
rem Uso: build_launcher.bat https://raw.githubusercontent.com/UTENTE/REPO/main/manifest.json
if "%~1"=="" (echo Indica l'URL del manifest come primo argomento.& exit /b 1)
cd /d "%~dp0"
powershell -NoProfile -Command "(Get-Content Launcher.cs -Raw -Encoding UTF8).Replace('__MANIFEST_URL__','%~1') | Set-Content _build.cs -Encoding UTF8"
set RES=
if exist banner.jpg set RES=/resource:banner.jpg
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:AniimoPatchITA.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll %RES% _build.cs
del _build.cs
