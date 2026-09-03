# PowerDimmer

[![Main workflow](https://github.com/shayne/PowerDimmer/actions/workflows/workflow.yml/badge.svg)](https://github.com/shayne/PowerDimmer/releases/tag/main)

A simple distraction dimmer for Windows

| [<img src="https://user-images.githubusercontent.com/79330/147771591-853256ae-f4f1-42d3-8c68-ea467febeb58.png" width="800" />](https://user-images.githubusercontent.com/79330/147771591-853256ae-f4f1-42d3-8c68-ea467febeb58.png) |
| :--: |
| *Dim everything but focused window* |

| [<img src="https://user-images.githubusercontent.com/79330/147770555-5efe9efc-88e1-438e-a559-47b5f495976b.png" width="800" />](https://user-images.githubusercontent.com/79330/147770555-5efe9efc-88e1-438e-a559-47b5f495976b.png) |
| :--: |
| *Multiple focused windows via dimming toggle hotkey* |

## Features

Initial Release

* Dims all but currently focused window
* Toggle dimming for a specific window via `Win + Shift + D`
* Adjust brightness level from the system tray context menu

New features
* Toggle shade for a specific window via `Win + Alt + S`, usefull for that one bright screen without darkmode
* Shade an area of a window via `Win + Alt + A` then select the area to shade. The shade will move with the window

## Building

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and run:

```powershell
dotnet run
```

## Standalone Release

Create a self-contained 64-bit Windows release with:

```powershell
dotnet publish -c Release
```

The output is in `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish`. Distribute the complete contents of that folder. It includes the required .NET runtime, so users do not need to install one separately.

## Context

I threw this project together after finding my own need PowerDimmer. I initially tried LeDimmer. It works well enough, but it's abandoned, closed source and very out of date.

A fan of PowerToys I checked for an open issue and found [microsoft/PowerToys#13035](https://github.com/microsoft/PowerToys/issues/13035). I decided to write this to try and gain support and get this module added to PowerToys.