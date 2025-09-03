# YimMenu Launchpad

A modern WPF launcher and injector application for YimMenu ([legacy](https://github.com/Mr-X-GTA/YimMenu) and [enhanced](https://github.com/YimMenu/YimMenuV2)), designed to simplify the process of launching GTA V and injecting the YimMenu mod menu.

## Features

- **Multi-Platform Support**: Launch GTA V through ~~Epic Games~~, Steam, or ~~Rockstar Games Launcher~~
- **Automatic Updates**: Automatically checks for and downloads the latest YimMenu releases
- **Smart Process Detection**: Monitors game process status and adapts UI accordingly
- **One-Click Injection**: Simple DLL injection into the running GTA V process
- **Modern Dark UI**: Clean, modern interface with Windows 11 styling
- **Configuration Persistence**: Remembers your platform and version preference

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.19/windowsdesktop-runtime-8.0.19-win-x64.exe)
- GTA V (Epic Games, Steam, or Rockstar Games version)
- Administrator privileges (in case of error)

## Download

### Pre-built Releases

Download the latest release from the [Releases](https://github.com/ExoticGamerrrYT/yimmenuv2-launchpad/releases) section.

### Build from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/ExoticGamerrrYT/yimmenuv2-launchpad.git
   ```
2. Navigate to the project directory:
   ```bash
   cd yimmenuv2-launchpad
   ```
3. Build the solution:
   ```bash
   dotnet build --configuration Release
   ```

## Installation

1. Download or build the application
2. Extract to your desired location
3. Run `YimMenu Launchpad.exe`
4. The application will automatically create necessary folders in `%APPDATA%\YimMenu Launchpad\`

## Usage

> Remember that BE **must** be disabled in your platform in order to use YimMenu

### First Time Setup

1. Launch the application
2. Select your preferred platform (~~Epic Games~~, Steam, or ~~Rockstar Games~~)

### Launching GTA V

1. Select your platform from the dropdown
2. Click **🚀 LAUNCH** to start GTA V
3. Wait for the game to fully load

### Injecting YimMenu

1. Ensure GTA V is running and in the main menu
2. Click **💉 INJECT** to automatically update and inject YimMenu into the game process
3. The button will be highlighted when the game is detected

### Additional Features

- **📋 CHANGELOG**: View the latest release notes
- **📁 OPEN YIMENU FOLDER**: Open the YimMenuV2 configuration folder

## File Structure

```
%APPDATA%\YimMenu Launchpad\
├── YimMenu/YimMenuV2.dll      # The mod menu DLL
├── hash_v1/hash_v2.txt        # File hash for update verification
├── launchpad_config.txt       # Application configuration
```

## Troubleshooting

### Common Issues

**"Process not found" error:**

- Ensure GTA V is fully loaded (not just starting up)
- Make sure you're using the correct platform (Epic/Steam/Rockstar)
- Try running the launchpad as administrator

**"DLL not found" error:**

- Check that antivirus isn't blocking the download and re-inject

**Injection fails:**

- Check if BE is disabled for you platform
- Run the launchpad as administrator
- Temporarily disable antivirus software
- Ensure no other mod menus are already injected

**Game won't launch:**

- Verify the game platform is correctly selected
- Check that the respective launcher (Steam/Epic/Rockstar) is installed
- Try launching the game manually first

### Getting Help

- Check the [Issues](https://github.com/ExoticGamerrrYT/yimmenuv2-launchpad/issues) section
- Review the [YimMenuV2 documentation](https://github.com/YimMenu/YimMenuV2)

## Contributing

We welcome contributions! Here's how you can help:

### Reporting Issues

1. Check existing [issues](https://github.com/ExoticGamerrrYT/yimmenuv2-launchpad/issues) first
2. Create a new issue with:
   - Clear description of the problem
   - Steps to reproduce
   - System information (Windows version, .NET version)
   - Screenshots if applicable

### Code Contributions

1. Fork the repository
2. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Make your changes following the existing code style
4. Format your code:
   ```bash
   dotnet csharpier format .
   or
   ./format.ps1 (from root)
   ```
5. Test your changes thoroughly
6. Commit with clear, descriptive messages
7. Push to your fork and create a Pull Request

### Development Setup

1. Install Visual Studio 2022 (.NET Desktop workload) or VS Code with C# extension
2. Install .NET 8.0 SDK
3. Clone your fork of the repository
4. Open the solution file in your IDE
5. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

### Code Style

- Follow existing code conventions
- Use meaningful variable and method names
- Add comments for complex logic
- Format code using CSharpier before submitting

### Pull Request Guidelines

- Target the `main` branch
- Include a clear description of changes
- Reference any related issues
- Ensure all builds pass
- Update documentation if needed

## Roadmap

- [x] Add support for YimMenuV1 (legacy gta)
- [ ] Add BE is not disabled message in launcher when injection fails
- [ ] Epic Games Store launcher integration
- [ ] Rockstar Games Launcher integration
- [ ] Automatic game detection and launching

## License

<a href="https://github.com/ExoticGamerrrYT/yimmenuv2-launchpad">YimMenu Launchpad</a> © 2025 by <a href="https://github.com/ExoticGamerrrYT">ExoticGamerrrYT</a> is licensed under <a href="https://creativecommons.org/licenses/by-nc/4.0/">CC BY-NC 4.0</a><img src="https://mirrors.creativecommons.org/presskit/icons/cc.svg" alt="" style="max-width: 1em;max-height:1em;margin-left: .2em;"><img src="https://mirrors.creativecommons.org/presskit/icons/by.svg" alt="" style="max-width: 1em;max-height:1em;margin-left: .2em;"><img src="https://mirrors.creativecommons.org/presskit/icons/nc.svg" alt="" style="max-width: 1em;max-height:1em;margin-left: .2em;">

## Disclaimer

This project is not affiliated with YimMenu or Take-Two Interactive. Use at your own risk. The developers are not responsible for any consequences of using this software, including but not limited to game bans or account suspensions.

## Acknowledgments

- [YimMenu Team](https://github.com/YimMenu) for creating YimMenu
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) for JSON parsing
