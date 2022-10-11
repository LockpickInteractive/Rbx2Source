# ![rbx2source](Resources/Images/smallRbx2SourceLogo.png)
### Port Roblox models to Source Engine Games
##
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FStarLandRBLX%2FRbx2Source&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)[![Build Rbx2Source](https://github.com/StarLandRBLX/Rbx2Source/actions/workflows/build.yml/badge.svg)](https://github.com/StarLandRBLX/Rbx2Source/actions/workflows/build.yml)[![CodeQL](https://github.com/StarLandRBLX/Rbx2Source/actions/workflows/codeql.yml/badge.svg)](https://github.com/qfoxb/Rbx2Source-gh-actions-test/actions/workflows/codeql.yml)![Discord](https://discordapp.com/api/guilds/787797824557154344/widget.png?style=shield)                         
Originally developed by [MaximumADHD](https://github.com/MaximumADHD), This project serves as a continuation of the original [Rbx2Source](https://github.com/MaximumADHD/Rbx2Source) Project.

# Setup
- Download the latest [Rbx2Source.exe](https://github.com/StarLandRBLX/Rbx2Source/raw/main/Rbx2Source.exe) file from the github page. 
- Alternatively, download the latest build artifact from the last commit.      
- If your Source Games are on a drive other than C:, you must create a config.txt file next to Rbx2Source.
- [Here's an example of a good config.txt file](https://github.com/StarLandRBLX/Rbx2Source/raw/main/config.example.txt)
- If you're having issues, try running the [Cache Clearer](https://github.com/StarLandRBLX/Rbx2Source/raw/main/Clear%20Cache.bat).  If that doesn't fix anything, create a github issue.

# Building Instructions
- Clone the github repository.
- Clone [This repository](https://github.com/MaximumADHD/Roblox-File-Format/) as well.
- Restore NuGet Packages on both repositories
- Build within Visual Studio 2022

# Known Bugs
- Not loading thumbnails of users/accessories [issue #1](https://github.com/StarLandRBLX/Rbx2Source/issues/1)