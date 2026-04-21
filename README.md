# MacroscopTest

WPF application for loading images by URL with async operations and cancellation support.

## Overview

The application allows:
- loading images into three independent slots;
- cancelling any active download;
- starting all downloads at once;
- tracking the number of active downloads via a progress bar.

The UI remains responsive during all operations.

## Features

- 3 independent image slots
- separate URL input for each slot
- `START` / `STOP` buttons per slot
- `LOAD ALL` button
- shared progress bar (0–3 active downloads)
- individual status and error handling per slot
- simple file logging (`Logs` folder)

## Architecture

The project follows MVVM architecture.

- `MainViewModel` manages the overall screen and progress
- `ImageSlotViewModel` handles a single slot state and commands
- `ImageDownloadService` downloads images and creates `BitmapImage`
- `FileLogger` writes logs to file
- `AsyncCommand` and `DelegateCommand` handle user actions

## Implementation Details

- uses `async/await` without blocking UI
- each slot has its own `CancellationTokenSource`
- cancellation works independently per slot
- `LOAD ALL` runs downloads in parallel
- prevents outdated results from overriding current state
- images are created with `BitmapImage` (`OnLoad`, `Freeze()`)
- large images are downscaled to reduce memory usage

## How to Run

1. Open `MacroscopTest.sln` in Visual Studio 2022
2. Make sure .NET 6 SDK is installed
3. Set `MacroscopTest` as startup project
4. Run the application

## Tech Stack

- C#
- .NET 6
- WPF
- MVVM
- async/await
- CancellationToken
- MSTest

## Notes

- Logs are written to the `Logs` folder next to the application
- Unit tests are located in `MacroscopTest.Tests`