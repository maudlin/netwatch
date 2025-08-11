# Netwatch

A lightweight Windows desktop app that gives you a live heads-up display (HUD) of your network status. Netwatch provides a minimal always-on-top mini HUD and an expanded window for deeper insight, so you can keep an eye on your connection while working, streaming, or gaming.

If you just want to know “is my connection healthy?” without digging through system dialogs, this is for you.


## What you can do (for users)
- Mini HUD overlay: a compact, always-on-top readout designed to stay out of your way.
- Expanded view: a larger window with more detail when you need it.
- Status at a glance: see current connection state and key indicators (e.g., activity, throughput). 
- Designed for Windows 10/11.

Planned/under consideration:
- Adapter selection and quick switching
- Latency checks to common endpoints
- Alerts when connectivity drops or degrades
- Simple theme options (light/dark, opacity)


## Getting started
Prerequisites:
- Windows 10/11
- .NET 8 Desktop Runtime (or install the .NET 8 SDK if you plan to build from source)

Run from source (command line):
- dotnet build
- dotnet run

Run from Visual Studio:
- Open networkhud.sln
- Set configuration to Debug or Release
- Press F5 (Debug) or Ctrl+F5 (Run)


## Using the app
- Mini HUD: a small always-on-top window that can be positioned to taste.
- Expanded window: open for details when you want more context.
- Close/Exit from the window controls.

As features solidify, this section will list keyboard shortcuts, settings, and customization options.


## Technical details (for developers)
- Tech stack: .NET 8 (net8.0-windows), WPF (XAML) with some Windows Forms interop enabled.
- Project: Netwatch.csproj
- Solution: networkhud.sln
- Targets: WinExe (Windows-only)
- UI resources: XAML styles in Themes/Theme.xaml

Project structure (high-level):
- App.xaml, App.xaml.cs: application startup and merged resource dictionaries.
- Controls/: reusable UI controls.
- Models/: data models.
- Services/: runtime services (e.g., system/network queries).
- Themes/: XAML resources (styles, fonts, colors).
- MiniHudWindow.xaml(.cs): compact overlay view.
- ExpandedWindow.xaml(.cs): detailed view.

Build:
- dotnet build
- Artifacts: bin/ and obj/ (ignored by .gitignore)

Packaging (future):
- Consider dotnet publish -p:PublishSingleFile=true -r win-x64 for a self-contained build.


## Screenshots
A draft mockup is included in the repo:
- mockup.png


## Development notes
- Target framework is net8.0-windows with UseWPF enabled.
- Implicit usings and nullable are enabled in the project.
- UseWindowsForms is enabled for potential interop.
- Theme.xaml is merged in App.xaml to provide consistent fonts and styles.


## Roadmap / TODO
- Implement/finish mini HUD metrics (throughput, connectivity, adapter).
- Add expanded view with richer charts or diagnostics.
- Adapter selection and configuration UI.
- Optional latency probing and alerting.
- Settings persistence and basic theming.
- Packaging for releases.


## Contributing
Issues and PRs are welcome. If you plan larger changes, please open an issue first to discuss the approach.


## License
TBD. Add a LICENSE file to clarify usage rights before first release.

