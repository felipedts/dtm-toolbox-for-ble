# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- 2-wire UART protocol layer: command encoding, event decoding, RF channel mapping, Nordic nRF5x constant carrier command.
- Device layer: serial link, device operations with a per-frame exchange event, test plan and test runner for single channel and sweep, transmitter and receiver.
- Serial port enumeration from Windows with friendly names, and a device change watcher for plug and unplug.
- User interface: serial port selector, test settings for single channel and sweep, transmitter chart with the transmit power applied by the device, receiver chart with packets per channel, log pane with optional raw frames and export to a text file, about pane with the vendor extension setting and the device identification.
- Settings kept between runs in `%APPDATA%\DtmToolbox\settings.xml`.
- Transmit power on firmware that predates Bluetooth 5.2: when the device rejects the setup command `0x09`, the Nordic vendor command is used with the nearest level the radio accepts.
- Log lines can be selected and copied (Ctrl+C, context menu, COPY LOG). The error message and the about texts can be selected too.
- Unexpected errors are shown on screen and written to `%APPDATA%\DtmToolbox\crash.log`.
- `--simulate` command line switch: adds two simulated devices, with current and with legacy firmware, to run the application without hardware.
- Unit tests for the protocol, the device operations, the test runner, the settings store and the view model.
- Protocol reference in `docs/protocol.md`.
