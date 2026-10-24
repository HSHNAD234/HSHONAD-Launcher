# HSHONAD Launcher

Windows launcher source project built with .NET 10 / Windows Forms.

## Source layout

The updated source archive currently contains the Visual Studio project, launcher UI and logic, update installer, local proxy, web UI, GitHub Actions workflows, and the optional `HSHONAD.UpdateApi` service.

## Build

Open `HSHONAD Launcher/HSHONAD Launcher.csproj` in Visual Studio with the .NET 10 SDK and the Windows desktop development workload installed.

## Release updates

The launcher updater expects a GitHub Release asset named `HSHONAD-Launcher-win-x64.zip`. Configure the release workflow and repository settings before relying on automatic updates.

## Important

Do not commit credentials, local configuration secrets, signing certificates, or generated build output. The source ZIP has not yet been validated by a .NET build in this environment.