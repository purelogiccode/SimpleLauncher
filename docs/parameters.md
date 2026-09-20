# List of Parameters to use in the "system.xml"

## Amstrad CPC

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Amstrad CPC<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** dsk, ipf, voc, cdt, cpr, sna<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Retroarch caprice32<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\cap32_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\cap32_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/caprice32/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** CPCEC<br>
**Emulator Path (Example):** D:\Emulators\CPCEC\CPCEC.EXE<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/cpcitor/cpcec).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example for floppy disk images - .dsk):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Amstrad CPC;%SYSTEMFOLDER%" cpc6128 -flop1<br>
**Emulator Parameters (Example for floppy disk images - .dsk):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Amstrad CPC;%SYSTEMFOLDER%" cpc464 -flop1<br>
**Emulator Parameters (Example for tape images - .cdt, .wav):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Amstrad CPC;%SYSTEMFOLDER%" cpc464 -cass<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Amstrad GX4000

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Amstrad CPC GX4000<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Amstrad GX4000" gx4000 -cart<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Amstrad GX4000;%SYSTEMFOLDER%" gx4000 -cart<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List Roms. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Arcade

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Emulators\mame\roms<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\mame\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\mame\roms;C:\Emulators\mame\bios"<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Arcade;%SYSTEMFOLDER%"<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find an updated set of MAME ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

.

**Emulator Name:** Retroarch mame<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mame_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mame_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mame_2010/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Raine<br>
**Emulator Path (Example):** C:\Emulators\Raine\raine.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** -fs 1<br>

This emulator is available for Windows-x64.<br>
You need to use a ROM set compatible with Raine. You can find an updated set of Raine ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/nonmame/raine/index.html) website. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://raine.1emulation.com/download/latest.html).<br>

## Atari 2600

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari 2600<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Stella<br>
**Emulator Path (Example):** C:\Emulators\stella\Stella.exe<br>
**Emulator Parameters (Example):** -fullscreen 1<br>
**Fullscreen Parameter:** -fullscreen 1<br>

This emulator is available for Windows-x64.<br>
Command line documentation can be found on [Stella Website](https://stella-emu.github.io/docs/index.html#CommandLine).<br>
You can download this emulator on the [Official Website](https://stella-emu.github.io/downloads.html).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Atari 2600"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch stella<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\stella_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\stella_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/stella/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari 2600" a2600<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari 2600;%SYSTEMFOLDER%" a2600<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atari 5200

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari 5200<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** ATR, ATX, ATZ, DCM, XFD, PRO, ARC, BAS, ROM, BIN, A52, CAS, SAP<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Altirra<br>
**Emulator Path (Example):** C:\Emulators\altirra\Altirra64.exe<br>
**Emulator Parameters (Example):** /f /cart<br>
**Fullscreen Parameter:** /f<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://www.virtualdub.org/altirra.html).<br>

.

**Emulator Name:** Retroarch a5200<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\a5200_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\a5200_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/atari800/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari 5200" a5200<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari 5200;%SYSTEMFOLDER%" a5200<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atari 7800

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari 7800<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Retroarch prosystem<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\prosystem_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\prosystem_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/prosystem/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** ProSystem<br>
**Emulator Path (Example):** C:\Emulators\ProSystem\ProSystem.exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/gstanton/ProSystem1_3).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari 7800" a7800<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari 7800;%SYSTEMFOLDER%" a7800<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atari 8-Bits / Atari 800

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari 8-Bits<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** ATR, ATX, ATZ, DCM, XFD, PRO, ARC, BAS, ROM, BIN, A52, CAS, SAP<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Altirra<br>
**Emulator Path (Example):** C:\Emulators\altirra\Altirra64.exe<br>
**Emulator Parameters:** /f<br>
**Fullscreen Parameter:** /f<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://www.virtualdub.org/altirra.html).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari 800" a800<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari 800;%BASEFOLDER%\roms\Atari 8-Bits;%SYSTEMFOLDER%" a800<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

.

**Emulator Name:** Retroarch atari800_libretro<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\atari800_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\atari800_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/atari800/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Atari Jaguar

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari Jaguar<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** j64, jag, rom, abs, cof, bin, prg<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** BigPEmu<br>
**Emulator Path (Example):** C:\Emulators\bigpemu\BigPEmu.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://richwhitehouse.com/jaguar/index.php?content=download).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari Jaguar" jaguar<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari Jaguar;%SYSTEMFOLDER%" jaguar<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atari Jaguar CD

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari Jaguar CD<br>
**Extension to Search in the System Folder:** zip, 7z, cue, cdi<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** cue, cdi<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** BigPEmu<br>
**Emulator Path (Example):** C:\Emulators\BigPEmu\BigPEmu.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://richwhitehouse.com/jaguar/index.php?content=download).<br>

## Atari Lynx

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari Lynx<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** lnx, o<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
May require BIOS file to work.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Gearlynx<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\Gearlynx.exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to run.<br>
You can download this emulator on the [Official Website](https://github.com/drhelius/Gearlynx).<br>

.

**Emulator Name:** Retroarch mednafen_lynx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_lynx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_lynx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_lynx/).<br>
Core requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari Lynx" lynx<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari Lynx;%SYSTEMFOLDER%" lynx<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atari ST

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Atari ST<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true or false (depending on the emulator you are using)<br>
**Extension to Launch After Extraction:** msa, st, stx, dim, ipf, IMG, RAW, VHD, HDV, PRG, TOS, TTP, APP, ROM<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Hatari<br>
**Emulator Path (Example):** C:\Emulators\hatari\hatari.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** -f or --fullscreen<br>
**Windowed Parameter:** -w or --window<br>

This emulator is available for Windows-x64.<br>
You can find a list of command-line args on the [Hatari Website](https://www.hatari-emu.org/doc/manual.html#Command_line_options_and_arguments).<br>
Emulator may require BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://www.hatari-emu.org/download.html).<br>

.

**Emulator Name:** Steem SSE<br>
**Emulator Path (Example):** C:\Emulators\Steem SSE\Steem64D3D.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator require BIOS to work.<br>
You can download this emulator on the [SourceForge Repository](https://sourceforge.net/projects/steemsse/).<br>

.

**Emulator Name:** Retroarch hatari<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\hatari_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\hatari_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/hatari/).<br>
Core requires tos.img BIOS file to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Atari ST" st<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Atari ST;%SYSTEMFOLDER%" st<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Atomiswave / Sammy Atomiswave

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Atomiswave<br>
**Extension to Search in the System Folder:** chd, iso, zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Flycast<br>
**Emulator Path (Example):** C:\Emulators\Flycast\flycast.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/flyinghead/flycast).<br>

## Bandai WonderSwan

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Bandai WonderSwan<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "WonderSwan"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Retroarch mednafen_wswan<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_wswan_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_wswan_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_cygne/).<br>
Core may require BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\WonderSwan" wswan<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\WonderSwan;%SYSTEMFOLDER%" wswan<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Bandai WonderSwan Color

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Bandai WonderSwan Color<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "WonderSwan Color"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Retroarch mednafen_wswan<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_wswan_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_wswan_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_cygne/).<br>
Core may require BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\WonderSwan Color" wscolor<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\WonderSwan Color;%SYSTEMFOLDER%" wscolor<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Casio PV-1000

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Casio PV-1000<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\Casio PV-1000" pv1000 -cart<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Casio PV-1000;%SYSTEMFOLDER%" pv1000 -cart<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Colecovision

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Colecovision<br>
**Extension to Search in the System Folder:** zip, 7z<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** col, cv, bin, rom<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Gearcoleco<br>
**Emulator Path (Example):** C:\Emulators\Gearcoleco\Gearcoleco.exe<br>
**Emulator Parameters (Example):** -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [GitHub Repository](https://github.com/drhelius/Gearcoleco).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "ColecoVision"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch gearcoleco<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\gearcoleco_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\gearcoleco_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/gearcoleco/).<br>
Core requires colecovision.rom BIOS file to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Colecovision" coleco<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Colecovision;%SYSTEMFOLDER%" coleco<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Commander Genius

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Commander Genius<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Commander Genius<br>
**Emulator Path (Example):** C:\Commander Genius\CGenius.exe<br>
**Emulator Parameters (Example):** dir="games/%NAME%"<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [GitLab Repository](https://gitlab.com/Dringgstein/Commander-Genius).<br>
You can download this emulator on the [Official Website](https://clonekeenplus.sourceforge.io/download.php).<br>

.

How to Set up Commander Genius<br>

Commander Genius stores its data (config, saves, game files) in `Documents\Commander Genius\` on Windows and `~/.CommanderGenius/` on Linux/macOS. Games belong in the `games\` subfolder. Simple Launcher will automatically extract compressed game archives to this official location and launch them.<br>

1. Install Commander Genius and extract it to a folder like `C:\Commander Genius`.<br>
2. Compress each game folder (e.g., `keen4\`, `keen5\`) into a `.zip` file. The game folder must contain the original Commander Keen data files (`.CK1` to `.CK6` extensions).<br>
3. Place the `.zip` files in your System Folder. The filename (without `.zip`) becomes the game folder name.<br>
4. Use the settings above in Simple Launcher. When you select a game, Simple Launcher will automatically:
   - Extract the `.zip` to `Documents\Commander Genius\games\<zipfilename>\` (`~/.CommanderGenius/games/<zipfilename>/` on Linux/macOS)<br>
   - Detect the game data folder inside the archive (even if nested)<br>
   - Launch CGenius with the correct `dir=` parameter and the official CG data path as working directory<br>
   - Clean up the extracted files after the game exits<br>
If you have issues, ensure the game data files inside the `.zip` use `.CK1`-`.CK6` extensions, and the archive contains a valid Commander Keen game folder.<br>

## Commodore 64

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Commodore 64<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true or false (depending on the emulator you are using)<br>
**Extension to Launch After Extraction:** arc, d64, d71, d81, g64, lnx, nbz, nib, prg, sda, sfx, t64, tap, 80, bin, crt<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** CCS64<br>
**Emulator Path (Example):** C:\Emulators\CCS64\CCS64.exe<br>
**Emulator Parameters (Example):**<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [CCS64 Website](https://www.ccs64.com/ccs64.html).<br>
You can download this emulator on the [Official Website](https://www.ccs64.com/).<br>

.

**Emulator Name:** Vice<br>
**Emulator Path (Example):** C:\Emulators\Vice\bin\x64sc.exe<br>
**Emulator Parameters (Autostart):** -autostart<br>
**Emulator Parameters (Attach and autoload tape/disk image):** -autoload<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>
You can find the list of command-line args on the [Vice Website](https://vice-emu.sourceforge.io/vice_toc.html).<br>
You can download this emulator on the [Official Website](https://vice-emu.sourceforge.io/index.html#download).<br>

.

**Emulator Name:** Retroarch vice_x64_libretro<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\vice_x64_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\vice_x64_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/vice/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch vice_x64sc_libretro<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\vice_x64sc_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\vice_x64sc_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/vice/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 64;%SYSTEMFOLDER%" c64<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 64;%SYSTEMFOLDER%" c64 -flop<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 64;%SYSTEMFOLDER%" c64 -cass<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 64;%SYSTEMFOLDER%" c64 -cart<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Commodore 128

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Commodore 128<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** arc, d64, d71, d81, g64, lnx, nbz, nib, prg, sda, sfx, t64, tap, 80, bin, crt<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Vice<br>
**Emulator Path (Example):** C:\Emulators\Vice\bin\x128.exe<br>
**Emulator Parameters (Autostart):** -autostart<br>
**Emulator Parameters (Attach and autoload tape/disk image):** -autoload<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>
You can find the list of command-line args on the [Vice Website](https://vice-emu.sourceforge.io/vice_toc.html).<br>
You can download this emulator on the [Official Website](https://vice-emu.sourceforge.io/index.html#download).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 128;%SYSTEMFOLDER%" c128<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 128;%SYSTEMFOLDER%" c128 -flop<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 128;%SYSTEMFOLDER%" c128 -cass<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore 128;%SYSTEMFOLDER%" c128 -cart<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

.

**Emulator Name:** Retroarch vice_x128<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\vice_x128_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\vice_x128_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/vice/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Commodore Amiga

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Commodore Amiga<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** adf, adz, dms, fdi, ipf, raw, hdf, hdz, directory, lha, slave, info, cur, ccd, chd, nrg, mds, iso, uae, m3u<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** WinUAE<br>
**Emulator Path (Example):** "C:\Emulators\WinUAE\WinUAE.exe"<br>
**Emulator Parameters (Example using relative paths):** /config "%EMULATORFOLDER%\Config.uae" /nogui /run<br>
**Emulator Parameters (Example using relative paths):** /config "%EMULATORFOLDER%\Config.uae" /nogui /floppy0<br>
**Emulator Parameters (Example using relative paths):** /config "%EMULATORFOLDER%\Config.uae" /nogui /harddrive<br>
**Emulator Parameters (Example using relative paths):** /config "%EMULATORFOLDER%\Config.uae" /nogui /cdrom<br>
**Fullscreen Parameter:** /fullscreen<br>
**Windowed Parameter:** /windowed<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.winuae.net/download/).<br>

.

**Emulator Name:** WinFellow<br>
**Emulator Path (Example):** <br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://github.com/petschau/WinFellow).<br>

.

**Emulator Name:** Retroarch puae<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\puae_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\puae_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/puae/) or [GitHub Repository](https://github.com/libretro/libretro-uae).<br>
Core requires BIOS files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch puae2021<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\puae2021_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\puae2021_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/puae/) or [GitHub Repository](https://github.com/libretro/libretro-uae).<br>
Core requires BIOS files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Commodore Amiga" a1200n<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a1200n<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a1000n<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a600n<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a500n<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a3000<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a2000<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a1200<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a1000<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a600<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga;%SYSTEMFOLDER%" a500<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

a3000: Amiga 3000<br>
a2000: Amiga 2000<br>
a1200: Amiga 1200<br>
a1000: Amiga 1000<br>
a600: Amiga 600<br>
a500: Amiga 500<br>
aXXXX: Represents a specific Amiga model with its default MAME configuration (often including hard drive support)<br>
aXXXXn: Represents a specific Amiga model configured specifically to not include a hard drive, ideal for floppy-only operations or network booting<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Commodore Amiga CD32

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Commodore Amiga CD32<br>
**Extension to Search in the System Folder:** chd, cue, ccd, nrg, mds, iso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Retroarch puae<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\puae_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\puae_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/puae/) or [GitHub Repository](https://github.com/libretro/libretro-uae).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch puae2021<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\puae2021_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\puae2021_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/puae/) or [GitHub Repository](https://github.com/libretro/libretro-uae).<br>
Core requires BIOS files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Commodore Amiga CD32" cd32<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Commodore Amiga CD32;%SYSTEMFOLDER%" cd32<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## FM Towns / FM-Towns

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\FM Towns<br>
**Extension to Search in the System Folder:** chd, cue<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Tsugaru<br>
**Emulator Path (Example):** C:\Emulators\FM Towns\Tsugaru_CUI.exe<br>
**Emulator Parameters:** "%EMULATORFOLDER%\roms" -CD "%ROM%" -FULLSCREEN<br>
**Fullscreen Parameter:** -FULLSCREEN<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will mount it in a virtual drive. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
This emulator requires BIOS and system files to work. These files need to be inside the roms folder.<br>
You can download this emulator on the [Official Website](https://github.com/captainys/TOWNSEMU/releases).<br>

## LaserDisk

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\LaserDisk<br>
**Extension to Search in the System Folder:** bat<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Daphne<br>
**Emulator Path:** <br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You need to create BAT files to launch the games.<br>
You can download this emulator on the [Official Website](http://www.daphne-emu.com/site3/index_hi.php).<br>

.

**Emulator Name:** Hypseus Singe<br>
**Emulator Path:** <br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You need to create BAT files to launch the games.<br>
You can download this emulator on the [Official Website](https://github.com/DirtBagXon/hypseus-singe).<br>

## Magnavox Odyssey 2

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Magnavox Odyssey 2<br>
**Extension to Search in the System Folder:** zip, 7z<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** bin<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Retroarch o2em<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\o2em_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\o2em_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/o2em/).<br>
Core requires BIOS files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** O2EM<br>
**Emulator Path (Example):** "C:\Emulators\O2EM\o2em.exe"<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://o2em.sourceforge.net/).<br>

## Mattel Aquarius

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Mattel Aquarius<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Mattel Aquarius" aquarius<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Mattel Aquarius;%SYSTEMFOLDER%" aquarius<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Mattel Intellivision

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Mattel Intellivision<br>
**Extension to Search in the System Folder:** zip, 7z<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** int, rom, bin<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Retroarch freeintv<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\freeintv_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\freeintv_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/freeintv/).<br>
Core requires BIOS files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Intellivision" intv<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Intellivision;%SYSTEMFOLDER%" intv<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Microsoft DOS

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Microsoft DOS<br>
**Extension to Search in the System Folder:** zip, 7z, rar<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** bat, conf, exe, com<br>
**Group Files by Folder:** false<br>

.

**When launching a compressed DOS game, Simple Launcher searches the extracted folder for game files in priority order:**<br>

**1.** `.conf` - A DOSBox configuration file. If found, it is used directly (launched with `-conf`).<br>
**2.** `.bat` - A batch file.<br>
**3.** `.exe` - An executable.<br>
**4.** `.com` - A command file.<br>

**If no `.conf` file is found, a temporary `.conf` is automatically generated that mounts the extracted folder as a virtual C: drive and runs the detected game file.**<br>
**You can also place uncompressed DOS game folders directly in the system folder (e.g., `C:\Microsoft DOS\Doom\`).** Include DOS executable extensions (e.g., `conf, bat, exe, com`) in `Extension to Search`, and set `Group Files by Folder` to `true`.<br>

.

**Emulator Name:** DOSBox<br>
**Emulator Path (Example):** C:\Emulators\DOSBox\DOSBox.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.dosbox.com/download.php?main=1).<br>

.

**Emulator Name:** DOSBox Staging<br>
**Emulator Path (Example):** C:\Emulators\DOSBox Staging\dosbox.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.dosbox-staging.org/releases/windows/).<br>

.

**Emulator Name:** DOSBox-X<br>
**Emulator Path (Example):** C:\Emulators\DOSBox-X\dosbox-x.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://dosbox-x.com/).<br>

.

**Emulator Name:** Retroarch dosbox_pure<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\dosbox_pure_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\dosbox_pure_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/dosbox_pure/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch dosbox<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\dosbox_core_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\dosbox_core_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/dosbox/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Microsoft MSX

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Microsoft MSX<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** rom, ri, mx1, mx2, col, dsk, cas, sg, sc, m3u, fdi<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** OpenMSX<br>
**Emulator Path (Example):** C:\Emulators\openmsx\openmsx.exe<br>
**Emulator Parameters:** -cart<br>
**Emulator Parameters:** -diska<br>
**Emulator Parameters:** -diskb<br>
**Emulator Parameters:** -cassetteplayer<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can find a list of parameters for this emulator on [OpenMSX Website](https://openmsx.org/manual/commands.html).<br>
You can download this emulator on the [Official Website](https://openmsx.org/).<br>

.

**Emulator Name:** MSXEC<br>
**Emulator Path (Example):** C:\Emulators\msxex\MSXEC.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/cpcitor/cpcec).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "MSX"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch bluemsx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\bluemsx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\bluemsx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/bluemsx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch fmsx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\fmsx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\fmsx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/fmsx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** fMSX<br>
**Emulator Path (Example):** C:\Emulators\fMSX\fMSX.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://fms.komkon.org/fMSX/).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Microsoft MSX" msx1<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Microsoft MSX;%SYSTEMFOLDER%" msx1<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Microsoft MSX2

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Microsoft MSX2<br>
**Extension to Search in the System Folder:** zip, 7z<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** rom, ri, mx1, mx2, col, dsk, cas, sg, sc, m3u<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** OpenMSX<br>
**Emulator Path (Example):** C:\Emulators\openmsx\openmsx.exe<br>
**Emulator Parameters:** -cart<br>
**Emulator Parameters:** -diska<br>
**Emulator Parameters:** -diskb<br>
**Emulator Parameters:** -cassetteplayer<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can find a list of parameters for this emulator on [OpenMSX Website](https://openmsx.org/manual/commands.html).<br>
You can download this emulator on the [Official Website](https://openmsx.org/).<br>

.

**Emulator Name:** MSXEC<br>
**Emulator Path (Example):** C:\Emulators\msxex\MSXEC.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/cpcitor/cpcec).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "MSX2"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/).<br>

.

**Emulator Name:** Retroarch bluemsx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\bluemsx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\bluemsx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/bluemsx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch fmsx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\fmsx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\fmsx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/fmsx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** fMSX<br>
**Emulator Path (Example):** C:\Emulators\fMSX\fMSX.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://fms.komkon.org/fMSX/).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Microsoft MSX2" msx2<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Microsoft MSX2;%SYSTEMFOLDER%" msx2<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Microsoft Windows

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Windows Games<br>
**Extension to Search in the System Folder:** lnk, bat, url, exe<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Direct Launch<br>
**Emulator Path:** <br>
**Emulator Parameters:** <br>

LNK files are shortcut files.<br>
You can create a shortcut by right-clicking on the Game.exe and selecting 'Create Shortcut'.<br>
If you prefer to use BAT files, use the tool available in the 'Simple Launcher' menu to generate BAT files for you.<br>

## Microsoft Xbox

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

.

**Emulator Name:** Xemu<br>
This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires BIOS and system files to work. The list of required files can be found on [Xemu Website](https://xemu.app/docs/required-files/).<br>
There are multiple ways to launch this emulator.

**Option 1 — Use CHD files (Recommended)**

**System Folder (Example):** c:\Xbox<br>
**Extension to Search in the System Folder:** chd<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Xemu\xemu.exe<br>
**Emulator Parameters (Example):** -full-screen -dvd_path<br>
**Fullscreen Parameter:** -full-screen<br>

You can find a tool in the 'Simple Launcher' tools menu that can batch convert ISOs to CHD format.<br>
'Simple Launcher' will mount the CHD file into a virtual drive, then load the game using a custom logic. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
You can download this emulator on the [Official Website](https://xemu.app/docs/download/).<br>

**Option 2 — Use ISO (XISO) files**

**System Folder (Example):** c:\Xbox<br>
**Extension to Search in the System Folder:** iso, xiso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Xemu\xemu.exe<br>
**Emulator Parameters (Example):** -full-screen -dvd_path<br>
**Fullscreen Parameter:** -full-screen<br>

You can find a tool in the 'Simple Launcher' tools menu that can batch convert ISOs to XISO format. This will reduce the ISO filesize and optimize the Redump ISO for emulation.<br>
You can download this emulator on the [Official Website](https://xemu.app/docs/download/).<br>

.

**Emulator Name:** Cxbx-Reloaded<br>
This emulator is available for Windows-x64.<br>
There are multiple ways to launch this emulator.

**Option 1 — Use CHD files (Recommended)**

**System Folder (Example):** c:\Xbox<br>
**Extension to Search in the System Folder:** chd<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Cxbx-Reloaded\cxbx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

'Simple Launcher' has logic to mount the CHD file into a virtual drive and then load the file default.xbe. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
You can find a tool in the 'Simple Launcher' tools menu that can batch convert ISO to CHD format.<br>
You can download this emulator on the [Official Website](https://cxbx-reloaded.co.uk/download).<br>

**Option 2 — Use ISO (XISO) files**

**System Folder (Example):** c:\Xbox<br>
**Extension to Search in the System Folder:** iso, xiso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Cxbx-Reloaded\cxbx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

'Simple Launcher' has logic to mount the ISO (XISO) file into a virtual drive and then load the file default.xbe. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
You can find a tool in the 'Simple Launcher' tools menu that can batch convert original Redump ISOs to XISO format. This will reduce the ISO filesize and optimize the Redump ISO for emulation.<br>
You can download this emulator on the [Official Website](https://cxbx-reloaded.co.uk/download).<br>

## Microsoft Xbox 360

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

.

**Emulator Name:** Xenia<br>
This emulator is available for Windows-x64.<br>
There are multiple ways to launch this emulator.

**Option 1 — Use CHD files (Recommended)**

**System Folder (Example):** c:\Xbox 360<br>
**Extension to Search in the System Folder:** chd<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Xenia\xenia.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

'Simple Launcher' will mount the CHD file into a virtual drive, then load the game using a custom logic. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
You can find a tool in the 'Simple Launcher' tools menu that can batch convert ISOs to CHD format.<br>
You can download this emulator on the [Official Website](https://github.com/xenia-project/release-builds-windows/releases).<br>

**Option 2 — Use ISO (XISO) files**

**System Folder (Example):** c:\Xbox 360<br>
**Extension to Search in the System Folder:** iso, xiso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Path (Example):** C:\Emulators\Xenia\xenia.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

You can find a tool in the 'Simple Launcher' tools menu that can batch convert original Redump ISOs to XISO format. This will reduce the ISO filesize and optimize the ISO for emulation.<br>
You can download this emulator on the [Official Website](https://github.com/xenia-project/release-builds-windows/releases).<br>

## Microsoft Xbox 360 XBLA

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

.

**Emulator Name:** Xenia<br>
This emulator is available for Windows-x64.<br>
There are multiple ways to launch this emulator.

**Option 1 - Use ZIP files (Recommended)**

**System Folder (Example):** c:\Microsoft Xbox 360 XBLA<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** Xenia<br>
**Emulator Path (Example):** C:\Emulators\Xenia\xenia.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

'Simple Launcher' will mount the ZIP file into a virtual drive, then load the game inside a nested '000D0000' folder. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
You can download this emulator on the [Official Website](https://github.com/xenia-project/release-builds-windows/releases).<br>

**Option 2 - Use [Game Folders]**

**System Folder (Example):** c:\Microsoft Xbox 360 XBLA<br>
**Extension to Search in the System Folder:** bat<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** Xenia<br>
**Emulator Path (Example):** C:\Emulators\Xenia\xenia.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

'Simple Launcher' has a tool in the Tools menu that can create [Batch Files For Xbox360 XBLA Games]. Use this tool to automatically create BAT files for you.<br>
You can download this emulator on the [Official Website](https://github.com/xenia-project/release-builds-windows/releases).<br>

## NEC PC Engine / TurboGrafx 16

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\NEC PC Engine<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "PC Engine"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/).<br>

.

**Emulator Name:** Ootake<br>
**Emulator Path (Example):** C:\Emulators\Ootake\Ootake.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 only.<br>
'Simple Launcher' will automatically extract compressed files to PCE extension, since this emulator does not support compressed files natively.<br>
You can download this emulator on the [Official Website](https://www.ouma.jp/ootake/index.html).<br>

.

**Emulator Name:** Retroarch mednafen_pce<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_pce_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_pce_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_pce_fast/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\NEC PC Engine" pce<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\NEC PC Engine;%SYSTEMFOLDER%" pce<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\NEC PC Engine" tg16<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\NEC PC Engine;%SYSTEMFOLDER%" tg16<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## NEC PC Engine CD / TurboGrafx CD

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\NEC PC Engine CD<br>
**Extension to Search in the System Folder:** chd, cue, ccd, iso, img, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
May require BIOS files to work.<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Geargrafx<br>
**Emulator Path (Example):** D:\Emulators\Geargrafx\Geargrafx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You may need a BIOS file to run this system on this emulator.<br>
You can download this emulator on the [Official Website](https://github.com/drhelius/Geargrafx).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\Ares\Ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "PC Engine CD"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Mesen<br>
**Emulator Path (Example):** C:\Emulators\Mesen\Mesen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will mount it in a virtual drive. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://github.com/SourMesen/Mesen2).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You need a BIOS file to run this system on this emulator. Please check documentation on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Retroarch mednafen_pce<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_pce_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_pce_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_pce_fast/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\NEC PC Engine CD" pcecd<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\NEC PC Engine CD;%SYSTEMFOLDER%" pcecd<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## NEC PC-FX / PC-FX / PCFX

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\NEC PC-FX<br>
**Extension to Search in the System Folder:** chd, cue, ccd, toc<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
May require a BIOS file to work.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You need a BIOS file to run this system on this emulator. Please check documentation on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Retroarch mednafen_pcfx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_pcfx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_pcfx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_pc_fx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\NEC PC-FX" pcfx<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\NEC PC-FX;%SYSTEMFOLDER%" pcfx<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## NEC SuperGrafx / SuperGrafx

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\NEC SuperGrafx<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "SuperGrafx"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch mednafen_supergrafx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_supergrafx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_supergrafx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_sgx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo 3DS

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo 3DS<br>
**Extension to Search in the System Folder:** 3ds, cci, 3dsx, elf, axf, cxi, app<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Azahar<br>
**Emulator Path (Example):** C:\Emulators\Azahar\azahar.exe<br>
**Emulator Parameters (Example):** -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://azahar-emu.org/pages/download/).<br>

.

**Emulator Name:** Borked3DS<br>
**Emulator Path (Example):** C:\Emulators\Borked3DS\borked3ds.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/Borked3DS/Borked3DS).<br>

.

**Emulator Name:** Citra<br>
**Emulator Path (Example):** C:\Emulators\citra\citra-qt.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://citra-emulator.com/download/).<br>

.

**Emulator Name:** Panda3DS<br>
**Emulator Path (Example):** C:\Emulators\panda3ds\Alber.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Documentation can be found on [GitHub](https://github.com/wheremyfoodat/Panda3DS).<br>
You can download this emulator on the [Official Website](https://panda3ds.com/download.html).<br>

.

**Emulator Name:** Retroarch citra<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\citra_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\citra_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/citra/).<br>
Core may require BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch panda3ds<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\panda3ds_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\panda3ds_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Documentation can be found on [GitHub](https://github.com/jonian/libretro-panda3ds) or [GitLab](https://git.libretro.com/libretro/Panda3DS).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo 64

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo 64<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** n64, v64, z64, bin, u1, ndd, gb<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Nintendo 64"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Simple64<br>
**Emulator Path (Example):** C:\Emulators\Simple64\simple64-gui.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/simple64/simple64).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Rosalie Mupen GUI - RMG<br>
**Emulator Path (Example):** C:\Emulators\Rosalie Mupen GUI\RMG.exe<br>
**Emulator Parameters (Example):** --fullscreen<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/rosalie241/RMG).<br>

.

**Emulator Name:** Gopher64<br>
**Emulator Path (Example):** C:\Emulators\Gopher64\gopher64-windows-x86_64.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/gopher64/gopher64).<br>

.

**Emulator Name:** Project64<br>
**Emulator Path (Example):** C:\Emulators\project64\Project64.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.pj64-emu.com/public-releases).<br>

.

**Emulator Name:** Retroarch mupen64plus_next<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mupen64plus_next_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mupen64plus_next_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mupen64plus/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch parallel_n64_libretro<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\parallel_n64_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\parallel_n64_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo 64" n64<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo 64;%SYSTEMFOLDER%" n64<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo 64DD

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo 64DD<br>
**Extension to Search in the System Folder:** zip, 7z<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** n64, v64, z64, bin, u1, ndd, gb<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Nintendo 64DD"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch mupen64plus_next<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mupen64plus_next_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mupen64plus_next_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mupen64plus/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo 64DD" n64dd<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo 64DD;%SYSTEMFOLDER%" n64dd<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo DS

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo DS<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** nds<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** melonDS<br>
**Emulator Path (Example):** C:\Emulators\melonDS\melonDS.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Emulator repository available on [GitHub](https://github.com/melonDS-emu/melonDS).<br>
You can download this emulator on the [Official Website](https://melonds.kuribo64.net/downloads.php).<br>

.

**Emulator Name:** DeSmuME<br>
**Emulator Path (Example):** C:\Emulators\DeSmuME\DeSmuME.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator repository available on [GitHub](https://github.com/TASEmulators/desmume).<br>
You can download this emulator on the [Official Website](https://github.com/TASEmulators/desmume).<br>

.

**Emulator Name:** NooDs<br>
**Emulator Path (Example):** C:\Emulators\NooDs\noods.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator repository available on [GitHub](https://github.com/Hydr8gon/NooDS).<br>
You can download this emulator on the [Official Website](https://github.com/hydr8gon/noods).<br>

.

**Emulator Name:** Retroarch melonds<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\melonds_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\melonds_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/melonds/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch desmume<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\desmume_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\desmume_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/desmume/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo Family Computer Disk System / Famicom Disk System

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Family Computer Disk System<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** nes, fds, unf, unif, qd<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Famicom Disk System"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Mesen<br>
**Emulator Path (Example):** C:\Emulators\mesen\Mesen.exe<br>
**Emulator Parameters (Example):** --fullscreen<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
This emulator requires a BIOS file to run this system.<br>
You can download this emulator on the [Official Website](https://github.com/SourMesen/Mesen2).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Retroarch mesen<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mesen_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mesen_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mesen/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch nestopia<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\nestopia_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\nestopia_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/nestopia_ue/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch fceumm<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\fceumm_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\fceumm_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core may require a BIOS file to work.<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/fceumm/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo Game Boy

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Game Boy<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false or true (depends on the emulator you are using)<br>
**Extension to Launch After Extraction:** gb, sgb, gbc, gba<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** mGBA<br>
**Emulator Path (Example):** C:\Emulators\mBGA\mGBA.exe<br>
**Emulator Parameters (Example):** -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://mgba.io/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Game Boy"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis).<br>

.

**Emulator Name:** Gearboy<br>
**Emulator Path (Example):** C:\Emulators\Gearboy\Gearboy.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://github.com/drhelius/Gearboy).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Sameboy<br>
**Emulator Path (Example):** C:\Emulators\Sameboy\sameboy.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
'Simple Launcher' will always extract compressed games when you use this emulator, since it does not support compressed files.<br>
You can download this emulator on the [Official Website](https://sameboy.github.io/).<br>

.

**Emulator Name:** Retroarch sameboy<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\sameboy_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\sameboy_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/sameboy/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch gambatte<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\gambatte_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\gambatte_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/gambatte/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch tgbdual<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\tgbdual_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\tgbdual_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/tgb_dual/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch gearboy<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\gearboy_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\gearboy_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/gearboy/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** TGB-Dual-L<br>
**Emulator Path (Example):** C:\Emulators\TGB-Dual-L\TGB_Dual_L (en).exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/libertyernie/tgbdual_L).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\Game Boy" gameboy<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Game Boy;%SYSTEMFOLDER%" gameboy<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo Game Boy Advance

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Game Boy Advance<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** mGBA<br>
**Emulator Path (Example):** C:\Emulators\mBGA\mGBA.exe<br>
**Emulator Parameters (Example):** -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://mgba.io/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Game Boy Advance"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Hades<br>
**Emulator Path (Example):** C:\Emulators\Hades\Hades.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [GitHub](https://github.com/hades-emu/hades).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://github.com/hades-emu/hades).<br>

.

**Emulator Name:** VisualBoy Advance M<br>
**Emulator Path (Example):** C:\Emulators\VisualBoy Advance M\visualboyadvance-m.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
You can download this emulator on the [Official Website](https://github.com/visualboyadvance-m/visualboyadvance-m).<br>

.

**Emulator Name:** Retroarch mgba<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mgba_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mgba_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mgba/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo Game Boy Advance" gba<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo Game Boy Advance;%SYSTEMFOLDER%" gba<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo Game Boy Color

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Game Boy Color<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false or true (depends on the emulator you are using)<br>
**Extension to Launch After Extraction:** gb, sgb, gbc, gba<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** mGBA<br>
**Emulator Path (Example):** C:\Emulators\mBGA\mGBA.exe<br>
**Emulator Parameters (Example):** -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://mgba.io/).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Game Boy Color"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis).<br>

.

**Emulator Name:** Gearboy<br>
**Emulator Path (Example):** C:\Emulators\Gearboy\Gearboy.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Emulator repository available on [GitHub](https://github.com/drhelius/Gearboy).<br>
You can download this emulator on the [Official Website](https://github.com/drhelius/gearboy).<br>

.

**Emulator Name:** Sameboy<br>
**Emulator Path (Example):** C:\Emulators\Sameboy\sameboy.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
'Simple Launcher' will always extract compressed games when you use this emulator, since it does not support compressed files.<br>
You can download this emulator on the [Official Website](https://github.com/LIJI32/SameBoy).<br>

.

**Emulator Name:** Retroarch sameboy<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\sameboy_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\sameboy_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/sameboy/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch gambatte<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\gambatte_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\gambatte_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/gambatte/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch tgbdual<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\tgbdual_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\tgbdual_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/tgb_dual/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch gearboy<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\gearboy_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\gearboy_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/gearboy/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** TGB-Dual-L<br>
**Emulator Path (Example):** C:\Emulators\TGB-Dual-L\TGB_Dual_L (en).exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/libertyernie/tgbdual_L).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\Nintendo Game Boy Color" gbcolor<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo Game Boy Color;%SYSTEMFOLDER%" gbcolor<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo GameCube

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo GameCube<br>
**Extension to Search in the System Folder:** rvz<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Dolphin<br>
**Emulator Path (Example):** C:\Emulators\dolphin\Dolphin.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://dolphin-emu.org/).<br>

.

**Emulator Name:** Retroarch dolphin<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\dolphin_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\dolphin_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/dolphin/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo NES / Famicom

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo NES<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Famicom"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** puNES<br>
**Emulator Path (Example):** C:\Emulators\puNES\punes64.exe<br>
**Emulator Parameters (Example):** --fullscreen yes<br>
**Fullscreen Parameter:** --fullscreen yes<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/punesemu/punes).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis).<br>

.

**Emulator Name:** Mesen<br>
**Emulator Path (Example):** C:\Emulators\mesen\Mesen.exe<br>
**Emulator Parameters (Example):** --fullscreen<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/SourMesen/Mesen2).<br>

.

**Emulator Name:** MyNes<br>
**Emulator Path (Example):** C:\Emulators\MyNes\MyNes.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Retroarch mesen<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mesen_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mesen_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/mesen/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch nestopia<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\nestopia_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\nestopia_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/nestopia_ue/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch fceumm<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\fceumm_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\fceumm_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core may require a BIOS file to work.<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/fceumm/).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo NES" nes<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo NES;%SYSTEMFOLDER%" nes<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo Satellaview

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Satellaview<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Super Famicom"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

## Nintendo SNES / Super Nintendo / Super Famicom / Super Nes

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo SNES<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** smc, sfc, swc, fig, bs, st<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Bsnes<br>
**Emulator Path (Example):** D:\Emulators\bsnes\bsnes.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/bsnes-emu/bsnes).<br>

.

**Emulator Name:** Snes9x<br>
**Emulator Path (Example):** C:\Emulators\snes9x\snes9x-x64.exe<br>
**Emulator Parameters (Example):** -fullscreen<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/snes9xgit/snes9x).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Super Famicom"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Retroarch snes9x<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\snes9x_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\snes9x_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/snes9x/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch bsnes<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\bsnes_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\bsnes_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_bsnes/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch bsnes-jg<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\bsnes-jg_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\bsnes-jg_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [GitHub Repository](https://github.com/libretro/bsnes-jg).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch mednafen_snes_libretro<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_snes_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_snes_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo SNES" snes<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo SNES;%SYSTEMFOLDER%" snes<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Nintendo SNES MSU1

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder:** c:\Nintendo SNES MSU1<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** smc, sfc, swc, fig, bs, st<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Snes9x<br>
**Emulator Path (Example):** C:\Emulators\snes9x\snes9x-x64.exe<br>
**Emulator Parameters (Example):** -fullscreen<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/snes9xgit/snes9x).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Super Famicom"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch snes9x<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\snes9x_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\snes9x_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/snes9x/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

## Nintendo Switch

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Switch<br>
**Extension to Search in the System Folder:** nsp, xci, nca, nro, nso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Eden<br>
**Emulator Path (Example):** C:\Emulators\Eden\eden.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://eden-emu.dev).<br>

.

**Emulator Name:** Citron<br>
**Emulator Path (Example):** C:\Emulators\Citron\citron.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Sudachi<br>
**Emulator Path (Example):** C:\Emulators\Sudachi\sudachi.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Yuzu<br>
**Emulator Path (Example):** C:\Users\HomePC\AppData\Local\yuzu\yuzu-windows-msvc\yuzu.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ryubing<br>
**Emulator Path (Example):** C:\Emulators\Ryubing\Ryujinx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Kenji-NX<br>
**Emulator Path (Example):** C:\Emulators\Kenji-Nx\Ryujinx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ryujinx<br>
**Emulator Path (Example):** C:\Emulators\Ryujinx\Ryujinx.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

## Nintendo Virtual Boy

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Virtual Boy<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>

.

**Emulator Name:** Retroarch mednafen_vb_libretro<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\Retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_vb_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_vb_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_vb/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Nintendo Virtual Boy" vboy<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Nintendo Virtual Boy;%SYSTEMFOLDER%" vboy<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## Nintendo Wii

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo Wii<br>
**Extension to Search in the System Folder:** rvz, elf, iso, gcm, dol, tgc, wbfs, ciso, gcz, wad<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Dolphin<br>
**Emulator Path (Example):** C:\Emulators\dolphin\Dolphin.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Retroarch dolphin<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\dolphin_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\dolphin_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/dolphin/).<br>
Core may require BIOS files or system files to work.<br>

## Nintendo WiiU

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo WiiU<br>
**Extension to Search in the System Folder:** wua, wux<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Cemu<br>
**Emulator Path (Example):** C:\Emulators\cemu\cemu.exe<br>
**Emulator Parameters (Example):** -f -g<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>

## Nintendo WiiWare

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Nintendo WiiWare<br>
**Extension to Search in the System Folder:** wad<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Dolphin<br>
**Emulator Path (Example):** C:\Emulators\dolphin\Dolphin.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Retroarch dolphin<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\dolphin_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\dolphin_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/dolphin/).<br>
Core may require BIOS files or system files to work.<br>

## Panasonic 3DO

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Panasonic 3DO<br>
**Extension to Search in the System Folder:** chd, cue, iso, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** 4DO<br>
**Emulator Path (Example):** C:\Emulators\4DO\4DO.exe<br>
**Emulator Parameters:** --StartFullScreen -StartLoadFile<br>
**Fullscreen Parameter:** --StartFullScreen<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will mount it in a virtual drive. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
This emulator requires a BIOS file to work.

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Retroarch opera<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\opera_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\opera_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/opera/).<br>
Core may require BIOS files or system files to work.<br>

## Philips CD-i

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Philips CD-i<br>
**Extension to Search in the System Folder:** chd, iso, cue, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** CDiEmu / CD-i Emulator<br>
**Emulator Path (Example):** C:\Emulators\CDiEmu\wcdiemu-v053b9.exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will mount it in a virtual drive. For this logic to work you need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany).<br>
Emulator may require BIOS or system files to work.

.

**Emulator Name:** Retroarch same_cdi<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\same_cdi_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\same_cdi_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/same_cdi/).<br>
Core may require BIOS files or system files to work.<br>

## ScummVM / Scumm-VM

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

.

**Emulator Name:** ScummVM<br>
This emulator is available for Windows-x64 and Windows-arm64.<br>
There are multiple ways to use this program.<br>

**Option 1 - Use ZIP files**

**System Folder (Example):** c:\ScummVM<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** ScummVM<br>
**Emulator Path:** c:\Emulators\ScummVM\scummvm.exe<br>
**Emulator Parameters:** --auto-detect --fullscreen<br>
**Fullscreen Parameter:** --fullscreen<br>

'Simple Launcher' will mount the ZIP file into a virtual drive, then load the game using a custom logic. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
Command line parameters can be found on [ScummVM Website](https://scumm-thedocs.readthedocs.io/en/latest/advanced/command_line.html#command-line-interface).

**Option 2 - Use [Game Folders]**

**System Folder (Example):** c:\ScummVM<br>
**Extension to Search in the System Folder:** bat<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** ScummVM<br>
**Emulator Path:** <br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

Use the tool available in the 'Simple Launcher' tools menu to generate BAT files for you.
Command line parameters can be found on [ScummVM Website](https://scumm-thedocs.readthedocs.io/en/latest/advanced/command_line.html#command-line-interface).

## Sega Dreamcast

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Dreamcast<br>
**Extension to Search in the System Folder:** chd, gdi, cdi, cue, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Flycast<br>
**Emulator Path (Example):** C:\Emulators\Flycast\flycast.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Redream<br>
**Emulator Path (Example):** C:\Emulators\redream\redream.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Demul<br>
**Emulator Path (Example):** C:\Emulators\Demul\demul.exe<br>
**Emulator Parameters:** -run=dc -image="%ROM%"<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Deecy<br>
**Emulator Path (Example):** C:\Emulators\Deecy\Deecy.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/Senryoku/Deecy).<br>

.

**Emulator Name:** Retroarch flycast<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\flycast_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\flycast_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/flycast/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Dreamcast" dc<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Dreamcast;%SYSTEMFOLDER%" dc<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.

## Sega Game Gear

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Game Gear<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MasterGear<br>
**Emulator Path (Example):** C:\Emulators\mastergear\MG.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
The list of commands available for this emulator can be found on [MasterGear Website](https://fms.komkon.org/MG/MG.html).<br>
You can download this emulator on the [Official Website](https://fms.komkon.org/MG/).<br>

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.carpeludum.com/kega-fusion/).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis/releases).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Emulicious<br>
**Emulator Path (Example):** C:\Emulators\Emulicious\Emulicious.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://emulicious.net/downloads/).<br>

.

**Emulator Name:** GearSystem<br>
**Emulator Path (Example):** C:\Emulators\GearSystem\Gearsystem.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Game Gear"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Retroarch genesis_plus_gx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\genesis_plus_gx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\genesis_plus_gx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/genesis_plus_gx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Game Gear" gamegear<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Game Gear;%SYSTEMFOLDER%" gamegear<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega Genesis / Mega Drive

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Genesis<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.carpeludum.com/kega-fusion/).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Mega Drive"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Blastem<br>
**Emulator Path (Example):** C:\Emulators\Blastem\blastem.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** ClownMDEmu<br>
**Emulator Path (Example):** C:\Emulators\ClownMDEmu\clownmdemu-v1.6.11-i686.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/Clownacy/clownmdemu-frontend).<br>

.

**Emulator Name:** Retroarch picodrive<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\picodrive_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\picodrive_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/picodrive/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch genesis_plus_gx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\genesis_plus_gx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\genesis_plus_gx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/genesis_plus_gx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch blastem<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\blastem_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\blastem_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/blastem/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Genesis" megadriv<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Genesis;%SYSTEMFOLDER%" megadriv<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega Genesis 32X / Mega Drive 32X

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Genesis 32X<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Mega 32X"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** Retroarch picodrive<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\picodrive_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\picodrive_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/picodrive/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Genesis 32x" 32x<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Genesis 32x;%SYSTEMFOLDER%" 32x<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega Genesis CD / Mega Drive CD / Sega CD / Mega CD

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Genesis CD<br>
**Extension to Search in the System Folder:** chd, cue, iso, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.
 
**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Mega CD"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
It requires a BIOS file to work.

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Gens<br>
**Emulator Path (Example):** C:\Emulators\Gens\gens.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
It requires a BIOS file to work.<br>

.

**Emulator Name:** Blastem<br>
**Emulator Path (Example):** C:\Emulators\Blastem\blastem.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
It requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://www.retrodev.com/blastem/).<br>

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
This emulator requires BIOS files to run Sega CD.<br>
You can download this emulator on the [Official Website](https://www.carpeludum.com/kega-fusion/).<br>

.

**Emulator Name:** Retroarch picodrive<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\picodrive_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\picodrive_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/picodrive/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch genesis_plus_gx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\genesis_plus_gx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\genesis_plus_gx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/genesis_plus_gx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Genesis CD" megacd<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Genesis CD;%SYSTEMFOLDER%" megacd<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega Master System / Mark3

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Master System<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MasterGear<br>
**Emulator Path (Example):** C:\Emulators\mastergear\MG.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
The list of command-line arguments available for this emulator can be found on [MasterGear Website](https://fms.komkon.org/MG/MG.html).<br>
You can download this emulator on the [Official Website](https://fms.komkon.org/MG/).<br>

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://www.carpeludum.com/kega-fusion/).<br>

.

**Emulator Name:** JGenesis<br>
**Emulator Path (Example):** C:\Emulators\JGenesis\jgenesis-cli.exe<br>
**Emulator Parameters (Example):** --file-path<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/jsgroth/jgenesis/releases).<br>

.

**Emulator Name:** GearSystem<br>
**Emulator Path (Example):** C:\Emulators\GearSystem\Gearsystem.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Master System"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
You can download this emulator on the [Official Website](https://tasvideos.org/Bizhawk).<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).<br>

.

**Emulator Name:** Retroarch genesis_plus_gx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\genesis_plus_gx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\genesis_plus_gx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/genesis_plus_gx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Master System" sms<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Master System;%SYSTEMFOLDER%" sms<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega Naomi

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Sega Naomi<br>
**Extension to Search in the System Folder:** chd, iso, zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Flycast<br>
**Emulator Path (Example):** C:\Emulators\Flycast\flycast.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/flyinghead/flycast).<br>

## Sega Naomi 2

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Sega Naomi2<br>
**Extension to Search in the System Folder:** chd, iso, zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Flycast<br>
**Emulator Path (Example):** C:\Emulators\Flycast\flycast.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/flyinghead/flycast).<br>

## Sega Model 3

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Model 3<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Supermodel<br>
**Emulator Path (Example):** C:\Emulators\Supermodel\Supermodel.exe<br>
**Emulator Parameters (Example):** -fullscreen -show-fps<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>

## Sega Saturn

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega Saturn<br>
**Extension to Search in the System Folder:** chd, cue, iso, ccd, mds, bin<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
May require a BIOS file to work.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).

.

**Emulator Name:** Ymir<br>
**Emulator Path (Example):** C:\Emulators\Ymir\ymir-sdl3.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
This program requires a BIOS file to work.<br>

.

**Emulator Name:** SSF<br>
**Emulator Path (Example):** C:\Emulators\SSF\SSF64.exe<br>
**Emulator Path (Example):** C:\Emulators\SSF\SSF.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Emulation General Wiki](https://emulation.gametechwiki.com/index.php/SSF).<br>
You can download this emulator on the [Official Website](https://segaretro.org/SSF).<br>

.

**Emulator Name:** Kronos<br>
**Emulator Path (Example):** C:\Emulators\Kronos\kronos.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** C:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Yaba Sanshiro<br>
**Emulator Path (Example):** C:\Emulators\Yaba Sanshiro\yabasanshiro.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Yabause<br>
**Emulator Path (Example):** C:\Emulators\Yabause\yabause.exe<br>
**Emulator Parameters (Example):** -f -a -i<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>

.

**Emulator Name:** Retroarch mednafen_saturn<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_saturn_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_saturn_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_saturn/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch kronos<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\kronos_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\kronos_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/kronos/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch yabasanshiro<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\yabasanshiro_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\yabasanshiro_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/yabasanshiro/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** Retroarch yabause<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\yabause_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\yabause_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/yabause/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega Saturn" saturn -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Saturn;%SYSTEMFOLDER%" saturn -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Saturn;%SYSTEMFOLDER%" saturnjp -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Saturn;%SYSTEMFOLDER%" saturnus -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega Saturn;%SYSTEMFOLDER%" saturneu -cdrom<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

saturn: Sega Saturn (World, BIOS 2.xx)<br>
saturnjp: Sega Saturn (Japan, BIOS 1.xx/2.xx)<br>
saturnus: Sega Saturn (USA, BIOS 2.xx)<br>
saturneu: Sega Saturn (Europe, BIOS 2.xx)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).<br>

## Sega SC-3000

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega SC-3000<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** sms, gg, sg, sc, sf, dsk, cht<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** MasterGear<br>
**Emulator Path (Example):** C:\Emulators\MasterGear\MG.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
The list of command-line arguments available for this emulator can be found on [MasterGear Website](https://fms.komkon.org/MG/MG.html).

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "SC-3000"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega SC-3000" sc3000<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega SC-3000;%SYSTEMFOLDER%" sc3000<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## Sega SG-1000

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sega SG-1000<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** sms, gg, sg, sc, sf, dsk, cht<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** MasterGear<br>
**Emulator Path (Example):** C:\Emulators\MasterGear\MG.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
The list of command-line arguments available for this emulator can be found on [MasterGear Website](https://fms.komkon.org/MG/MG.html).

.

**Emulator Name:** Kega Fusion<br>
**Emulator Path (Example):** C:\Emulators\Kega Fusion\Fusion.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** GearSystem<br>
**Emulator Path (Example):** C:\Emulators\GearSystem\Gearsystem.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64 and Windows-arm64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "SG-1000"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sega SG-1000" sg1000<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sega SG-1000;%SYSTEMFOLDER%" sg1000<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## Sharp x68000

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sharp x68000<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** XM6 Pro-68k<br>
**Emulator Path (Example):** C:\Emulators\XM6 Pro-68k\XM6.exe<br>
**Emulator Parameters (Example using absolute paths):** <br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Oficial Website](https://mijet.eludevisibility.org/XM6%20Pro-68k/XM6%20Pro-68k.html).<br>

.

**Emulator Name:** Retroarch px68k<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\px68k_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\px68k_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [libretro Website](https://docs.libretro.com/library/px68k/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\Sharp X68000" x68000 -flop1<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sharp X68000;%SYSTEMFOLDER%" x68000 -flop1<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## Sinclair ZX Spectrum

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sinclair ZX Spectrum<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** true<br>
**Extension to Launch After Extraction:** tzx, tap, z80, rzx, scl, trd, ipf<br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Spectral<br>
**Emulator Path (Example):** C:\Emulators\Spectral\Spectral-116.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:**

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Speccy<br>
**Emulator Path (Example):** C:\Emulators\Speccy\Speccy.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:**

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "ZX Spectrum"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).

.

**Emulator Name:** Retroarch fuse<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\fuse_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\fuse_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/fuse/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ZX Spectrum" spectrum<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\ZX Spectrum;%SYSTEMFOLDER%" spectrum<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## SNK Neo Geo

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\SNK Neo Geo<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\NeoGeo"<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\NeoGeo;%SYSTEMFOLDER%"<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find an updated set of MAME ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. I am not affiliated with PleasureDome in any way.

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Neo Geo AES"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
The list of command-line arguments available for this emulator can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).

.

**Emulator Name:** Raine<br>
**Emulator Path (Example):** C:\Emulators\Raine\raine.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** -fs 1<br>

This emulator is available for Windows-x64.<br>
You need to use a ROM set compatible with Raine. You can find an updated set of Raine ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/nonmame/raine/index.html) website. I am not affiliated with PleasureDome in any way.

.

**Emulator Name:** Retroarch geolith<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\Retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\Retroarch\Cores\geolith_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\geolith_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
Core documentation can be found on [docs.libretro.com](https://docs.libretro.com/library/geolith/).<br>
Core requires BIOS files (aes.zip and neogeo.zip) to work.<br>
This core cannot run MAME ROM sets. It uses ROM files with the extension .NEO.<br>

## SNK Neo Geo CD / NeoGeo CD

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\SNK Neo Geo CD<br>
**Extension to Search in the System Folder:** chd, cue<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\Ares\ares.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>

.

**Emulator Name:** Raine<br>
**Emulator Path (Example):** C:\Emulators\Raine\raine.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** -fs 1<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>

.

**Emulator Name:** FinalBurn Neo<br>
**Emulator Path (Example):** C:\Emulators\FinalBurn Neo\fbneo64.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>

.

**Emulator Name:** FinalBurn Alpha<br>
**Emulator Path (Example):** C:\Emulators\FinalBurn Alpha\fba64.exe<br>
**Emulator Parameters:** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>

.

**Emulator Name:** Retroarch neocd<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\neocd_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\neocd_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [NeoCD Repository](https://github.com/libretro/neocd_libretro).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\SNK Neo Geo CD" neocd<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\SNK Neo Geo CD;%SYSTEMFOLDER%" neocd<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.

## SNK Neo Geo Pocket

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\SNK Neo Geo Pocket<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Neo Geo Pocket"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.

.

**Emulator Name:** Retroarch mednafen_ngp<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_ngp_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_ngp_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_neopop/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** Retroarch race<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\race_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\race_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/race/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\SNK Neo Geo Pocket" ngp<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\SNK Neo Geo Pocket;%SYSTEMFOLDER%" ngp<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## SNK Neo Geo Pocket Color

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\SNK Neo Geo Pocket Color<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "Neo Geo Pocket Color"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.

.

**Emulator Name:** Retroarch mednafen_ngp<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_ngp_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_ngp_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_neopop/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** Retroarch race<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\race_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\race_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/race/).<br>
Core may require BIOS files or system files to work.<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\SNK Neo Geo Pocket Color" ngpc<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\SNK Neo Geo Pocket Color;%SYSTEMFOLDER%" ngpc<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.

## Sony PlayStation 1 / PSX 1

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sony PlayStation 1<br>
**Extension to Search in the System Folder:** chd, cue, bin, img, mds, mdf, pbp<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** true (if you are using the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) set of files for MAME Software List CHDs and using the MAME emulator)<br>

.

**Emulator Name:** DuckStation<br>
**Emulator Path (Example):** C:\Emulators\duckstation\duckstation-qt-x64-ReleaseLTCG.exe<br>
**Emulator Path (Example):** C:\Emulators\duckstation\duckstation-nogui-x64-ReleaseLTCG.exe<br>
**Emulator Parameters (Example):** -fullscreen<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Emulator documentation can be found on [DuckStation Repository](https://github.com/stenzek/duckstation).<br>
Emulator may need BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://github.com/stenzek/duckstation).

.

**Emulator Name:** Mednafen<br>
**Emulator Path (Example):** C:\Emulators\Mednafen\mednafen.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** -video.fs 1<br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
May require BIOS files to work.<br>
Emulator documentation can be found on [Mednafen Website](https://mednafen.github.io/documentation/).<br>
You can download this emulator on the [Official Website](https://mednafen.github.io/releases/).

.

**Emulator Name:** BizHawk<br>
**Emulator Path (Example):** D:\Emulators\BizHawk\EmuHawk.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You may need a BIOS file to run this system on this emulator. Please check on the [BizHawk Website](https://tasvideos.org/Bizhawk).<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Ares<br>
**Emulator Path (Example):** C:\Emulators\ares\ares-v146\ares.exe<br>
**Emulator Parameters (Example):** --fullscreen --system "PlayStation"<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
Command-line options can be found on [ares Repository](https://github.com/ares-emulator/ares/blob/master/README.md#command-line-options).<br>
This emulator requires a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://ares-emu.net/download).

.

**Emulator Name:** PCSX-Redux<br>
**Emulator Path (Example):** C:\Emulators\PCSX-Redux\pcsx-redux.exe<br>
**Emulator Parameters (Example):** -run -iso<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
This emulator may require a BIOS file to work.<br>
You can download this emulator on the [GitHub Repository](https://github.com/grumpycoders/pcsx-redux).

.

**Emulator Name:** ePSXe<br>
**Emulator Path (Example):** C:\Emulators\ePSXe\ePSXe.exe<br>
**Emulator Parameters (Example):** -loadiso<br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
This emulator may require a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://www.epsxe.com/download.php).

.

**Emulator Name:** Retroarch mednafen_psx<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\mednafen_psx_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\mednafen_psx_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/beetle_psx/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).

.

**Emulator Name:** Retroarch swanstation<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\swanstation_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\swanstation_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [GitHub](https://github.com/libretro/swanstation).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).

.

**Emulator Name:** Retroarch pcsx_rearmed<br>
**Emulator Path (Example):** C:\Emulators\Retroarch\Retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\Retroarch\cores\pcsx_rearmed_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\pcsx_rearmed_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [libretro.com](https://docs.libretro.com/library/pcsx_rearmed/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example using absolute paths):** -rompath "C:\Emulators\MAME\roms;C:\Emulators\MAME\bios;C:\ROMs\Sony PlayStation 1" psu -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sony PlayStation 1;%SYSTEMFOLDER%" psu -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sony PlayStation 1;%SYSTEMFOLDER%" psj -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sony PlayStation 1;%SYSTEMFOLDER%" pse -cdrom<br>
**Emulator Parameters (Example using relative paths):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Sony PlayStation 1;%SYSTEMFOLDER%" psa -cdrom<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

psu – Sony PlayStation (USA)<br>
psj – Sony PlayStation (Japan)<br>
pse – Sony PlayStation (Europe)<br>
psa – Sony PlayStation (Asia-Pacific)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List CHDs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).

## Sony PlayStation 2 / PSX2 / PSX 2

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sony PlayStation 2<br>
**Extension to Search in the System Folder:** chd, bin, iso, mdf, cso, zso, gz<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** PCSX2<br>
**Emulator Path (Example):** C:\Emulators\pcsx2\pcsx2-qt.exe<br>
**Emulator Parameters (Example):** -fullscreen<br>
**Fullscreen Parameter:** -fullscreen<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Documentation can be found on [PCSX2 Website](https://pcsx2.net/docs/).<br>
This emulator may require BIOS or system files to work.<br>
You can download this emulator on the [Official Website](https://pcsx2.net/downloads).

.

**Emulator Name:** Play<br>
**Emulator Path (Example):** C:\Emulators\Play\Play.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
This emulator may require a BIOS file to work.<br>
You can download this emulator on the [Official Website](https://purei.org/downloads.php).

.

**Emulator Name:** Retroarch pcsx2<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example using absolute paths):** -L "C:\Emulators\retroarch\cores\pcsx2_libretro.dll" -f<br>
**Emulator Parameters (Example using relative paths):** -L "%EMULATORFOLDER%\cores\pcsx2_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/pcsx2/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).

## Sony PlayStation 3 / PSX 3 / PSX3

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

.

**Emulator Name:** RPCS3<br>
This emulator is available for Windows-x64.<br>
There are multiple ways to use this emulator.<br>

**Option 1 — Use CHD files (Recommended)**

**System Folder (Example):** c:\Sony PlayStation 3<br>
**Extension to Search in the System Folder:** chd<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** RPCS3<br>
**Emulator Path (Example):** C:\Emulators\RPCS3\rpcs3.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen --no-gui<br>

If you want to use the '--fullscreen' parameter you also need to use the '--no-gui' parameter. This is a requirement imposed by this emulator.<br>
If you use a CHD file 'Simple Launcher' will automatically mount the CHD file before launching the emulator. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
Before converting your games from ISO to CHD ensure the ISO is decrypted.<br>

**Option 2 — Use decrypted ISO files**

**System Folder (Example):** c:\Sony PlayStation 3<br>
**Extension to Search in the System Folder:** iso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** RPCS3<br>
**Emulator Path (Example):** C:\Emulators\RPCS3\rpcs3.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen --no-gui<br>

If you want to use the '--fullscreen' parameter you also need to use the '--no-gui' parameter. This is a requirement imposed by this emulator.<br>

**Option 3 - Use [Game Folders]**

**System Folder (Example):** c:\Sony PlayStation 3<br>
**Extension to Search in the System Folder:** bat<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** RPCS3<br>
**Emulator Path (Example):** <br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen --no-gui<br>

If you want to use the '--fullscreen' parameter you also need to use the '--no-gui' parameter. This is a requirement imposed by this emulator.<br>
Use the tool available in the 'Simple Launcher' Tools menu to generate BAT files for you.<br>

**Option 4 - Use ZIP files**

**System Folder (Example):** c:\Sony PlayStation 3<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

**Emulator Name:** RPCS3<br>
**Emulator Path (Example):** C:\Emulators\RPCS3\rpcs3.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** --fullscreen --no-gui<br>

If you want to use the '--fullscreen' parameter you also need to use the '--no-gui' parameter. This is a requirement imposed by this emulator.<br>
'Simple Launcher' will mount the ZIP file into a virtual drive, then load the game using a custom logic. You need to install Dokan from [GitHub](https://github.com/dokan-dev/dokany) for the logic to work.<br>
You can download this emulator on the [Official Website](https://rpcs3.net/download).

## Sony PlayStation 4 / PSX4 / PSX 4

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sony PlayStation 4<br>
**Extension to Search in the System Folder:** bin, elf, oelf<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** shadPS4<br>
**Emulator Path (Example):** C:\Emulators\shadPS4\shadPS4.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [GitHub Repository](https://github.com/shadps4-emu/shadPS4).

## Sony PlayStation Vita

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sony PlayStation Vita<br>
**Extension to Search in the System Folder:** iso<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Vita3K<br>
**Emulator Path (Example):** C:\Emulators\Vita3K\Vita3K.exe<br>
**Emulator Parameters (Example):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://github.com/Vita3K/Vita3K).

## Sony PSP / PlayStation Portable

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Sony PSP<br>
**Extension to Search in the System Folder:** cso, chd, iso, pbp, elf, prx<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** PPSSPP<br>
**Emulator Path (Example):** C:\Emulators\ppsspp\PPSSPPWindows64.exe<br>
**Emulator Parameters (Example):** --fullscreen<br>
**Fullscreen Parameter:** --fullscreen<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>

.

**Emulator Name:** Retroarch ppsspp<br>
**Emulator Path (Example):** C:\Emulators\retroarch\retroarch.exe<br>
**Emulator Parameters (Example):** -L "C:\Emulators\retroarch\cores\ppsspp_libretro.dll" -f<br>
**Fullscreen Parameter:** -f<br>

This emulator is available for Windows-x64.<br>
It may support RetroAchievements. Please check on the [RetroAchievements Website](https://retroachievements.org/downloads).<br>
Core documentation can be found on [Libretro Website](https://docs.libretro.com/library/ppsspp/).<br>
Core may require BIOS files or system files to work.<br>
You can download this emulator on the [Official Website](https://www.retroarch.com/?page=platforms).

## Super ACan / Super-ACan / Super A'Can / Super-A'Can

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Super ACan<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example):** -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%BASEFOLDER%\roms\Super ACan;%SYSTEMFOLDER%" supracan -cart<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
To use this emulator you need to use a MAME compatible set of ROMs, with the right filenames.<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Look for MAME Software List ROMs. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).

## Tiger Game.com

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** c:\Tiger Game.com<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** MAME<br>
**Emulator Path (Example):** C:\Emulators\MAME\mame.exe<br>
**Emulator Parameters (Example):** -window -rompath "%EMULATORFOLDER%\roms;%EMULATORFOLDER%\bios;%SYSTEMFOLDER%" gamecom -cart1<br>
**Fullscreen Parameter:** -window (will load in windowed mode)<br>

This emulator is available for Windows-x64 and Windows-arm64.<br>
**Attention**<br>
The game filenames must follow the MAME naming convention (MAME Software List ROMs).<br>
You can find the right set of ROMs on the [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html) website. Download the 'Software List ROMs (split)' package and use the 'gamecom' folder. I am not affiliated with PleasureDome in any way.<br>
You can download this emulator on the [Official Website](https://www.mamedev.org/release.html).

## Zeebo

**Double-check file and folder paths when entering them in Simple Launcher.**<br>

**If you want to use relative paths, use the following placeholders:**<br>
**%BASEFOLDER%** - Represents the 'Simple Launcher' path<br>
**%SYSTEMFOLDER%** - Represents the System path<br>
**%EMULATORFOLDER%** - Represents the Emulator path<br>
**%ROM%** - Represents the full ROM path, with path and extension<br>
**%NAME%** - Represents the ROM name, without path or extension<br>
**%ROMSYSTEMFOLDER%** - Represents the System Folder that contains the selected ROM<br>

**System Folder (Example):** C:\Zeebo<br>
**Extension to Search in the System Folder:** zip<br>
**Extract File Before Launch?** false<br>
**Extension to Launch After Extraction:** <br>
**Group Files by Folder:** false<br>

.

**Emulator Name:** Infuse<br>
**Emulator Path (Example):** C:\Emulators\Infuse\Infuse.exe<br>
**Emulator Parameters (Example using relative paths):** <br>
**Fullscreen Parameter:** <br>

This emulator is available for Windows-x64.<br>
You can download this emulator on the [Official Website](https://tuxality.net/projects/infuse_zeebo_emulator).<br>