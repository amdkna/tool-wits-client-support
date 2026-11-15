# WITS Client Support

Windows helpdesk GUI written in **C# / .NET 10 / WPF**.

## First version (v0.1.0)

One **Check Internet Connection** button and a scrollable, timestamped log console.

The initial read-only checks include:

- Active network adapters and IPv4 addresses
- Default gateway ping
- Configured DNS server ping
- DNS resolution for google.com
- TCP connection to 1.1.1.1:443
- HTTPS request to https://www.google.com/generate_204

Pings may be blocked by local firewalls. The app does not modify any system configuration.

## Setup and run on a Windows development PC

Download the repository ZIP from GitHub and extract it, then double-click **setup.bat**.

Alternatively, with Git installed:

~~~powershell
git clone https://github.com/amdkna/tool-wits-client-support.git
cd tool-wits-client-support
.\setup.bat
~~~

The script:

1. Checks for Git, attempting installation through Windows Package Manager (winget) if missing.
2. Clones the repository if launched outside a Git checkout.
3. Fetches and fast-forwards to the latest main branch if the working directory is clean.
4. Checks for a .NET 10 SDK and installs it in the user profile if missing.
5. Publishes a self-contained Windows x64 executable.
6. Launches it.

Output: `publish\win-x64\WITS.ClientSupport.exe`

Options:

~~~powershell
.\setup.ps1 -NoRun
.\setup.ps1 -NoUpdate
~~~

If your company blocks internet downloads, install Git and .NET 10 SDK using approved offline installers, then run `setup.bat`.

The script checks for **latest Git source** on main, not GitHub Releases. It will not overwrite local modifications.

## Build manually

~~~powershell
dotnet publish src/Wits.ClientSupport/Wits.ClientSupport.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
~~~

The executable includes its .NET runtime, so clients do not need the .NET Desktop Runtime installed. Windows system libraries and WPF native dependencies remain OS-specific.

## Notes

- Target: Windows 10/11 x64 (test on your client fleet).
- Build requires Windows and the .NET 10 SDK.
- Network results are diagnostic indicators, not absolute proof of full internet availability.
- No GitHub Actions workflow is configured. Jenkins can be added when a Windows build agent is available.
