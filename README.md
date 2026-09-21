# DTM Toolbox for BLE

Desktop application for Bluetooth LE Direct Test Mode (DTM) over the 2-wire UART interface (Bluetooth Core Specification, Vol 6, Part F). It drives a device under test from a PC serial port: transmitter and receiver tests, single channel or channel sweep, with a live per-channel chart and a log of every frame exchanged.

Runs on Windows 7 SP1 and later. Distributed as an installer and as a portable executable.

## Features

- Transmitter test: packet type (PRBS9, 11110000, 10101010), payload length, PHY (LE 1M, LE 2M, LE Coded S2, LE Coded S8) and transmit power (Core 5.2 setup command `0x09`, with the level actually applied read back from the device).
- Receiver test: packets received per channel, accumulated across sweeps.
- Channel modes: single channel, or sweep over a channel range with a configurable dwell time. Optional test timeout.
- Vendor extension profiles: generic (specification only) or Nordic nRF5x (constant carrier).
- Serial port list taken from Windows, with friendly names and automatic refresh on plug and unplug.
- Log with timestamps and the raw command and event bytes, exportable to a text file.

## Requirements

- Windows 7 SP1 or later, x86 or x64.
- .NET Framework 4.6.2 or later. Windows 8.1, 10 and 11 ship with a compatible version. The installer carries the .NET Framework 4.8 redistributable and runs it only when the machine needs it.
- A device running DTM firmware with the 2-wire UART interface, reachable through a serial port (USB-to-serial adapter or on-board bridge). Default link settings: 19200 baud, 8N1, no flow control.

## Download

The installer (`DtmToolbox-<version>-setup.exe`) and the portable archive (`DtmToolbox-<version>-portable.zip`) are published on the Releases page. The portable executable runs from any folder and writes nothing outside `%APPDATA%\DtmToolbox`.

## Building from source

Requires the .NET SDK 8.0 on Windows. The .NET Framework 4.6.2 reference assemblies are restored automatically.

    dotnet build -c Release

Output: `src\DtmToolbox\bin\Release\net462\DtmToolbox.exe`.

## License

MIT. See [LICENSE](LICENSE).
