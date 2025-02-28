@echo off
setlocal enabledelayedexpansion

:: Define output file
set OUTPUT_FILE=launcher_assets/hashes.json
cd ..
:: Set folder to process (current directory)
set FOLDER=%CD%

:: Iterate over all files recursively
echo { >> %OUTPUT_FILE%
for /r "%FOLDER%" %%F in (*) do (
    :: Generate hash using CertUtil
    echo %%F
    for /f "tokens=1,2" %%A in ('certutil -hashfile "%%F" MD5 ^| findstr /v "hash"') do (
        set HASH=%%A
    )
    
    :: Format path and append to output file
    set FILEPATH=%%F
    set FILEPATH=!FILEPATH:%FOLDER%=!
    set FILEPATH=!FILEPATH:\=/!
    echo 	"!FILEPATH!": "!HASH!",>> %OUTPUT_FILE%
)
echo } >> %OUTPUT_FILE%

echo Hashes saved to %OUTPUT_FILE%.
endlocal
