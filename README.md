# rAthena Server Monitor

Updated to .NET 9.0 and made to run properly on windows 11. Launches processes, kills them correctly, and displays output for longer than 50 seconds!

![](https://i.imgur.com/9A6m9vz.png)

## Building

This project requires:
- .NET 9.0 SDK
- Windows operating system (targets `net9.0-windows7.0`)

To build the project:

```powershell
# Restore NuGet packages
nuget restore rAthena-Server-Monitor.sln

# Build in Release configuration
msbuild rAthena-Server-Monitor.sln -t:rebuild -p:Configuration=Release
```

The compiled application will be located in `rAthena-Server-Monitor\bin\Release\net9.0-windows7.0\`

## Releases

Releases are automatically built and packaged when a version tag is pushed:

```bash
git tag -a v1.1.1 -m "Release version 1.1.1"
git push origin v1.1.1
```

The GitHub Actions workflow will:
1. Build the application for .NET 9.0
2. Package it as a ZIP file
3. Create a GitHub release with the compiled artifacts

You can also manually trigger a release build from the Actions tab in GitHub.
