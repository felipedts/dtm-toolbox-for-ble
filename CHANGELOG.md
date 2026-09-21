# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- 2-wire UART protocol layer: command encoding, event decoding, RF channel mapping, Nordic nRF5x constant carrier command.
- Device layer: serial link, device operations with a per-frame exchange event, test plan and test runner for single channel and sweep, transmitter and receiver.
- Serial port enumeration from Windows with friendly names, and a device change watcher for plug and unplug.
- Unit tests for the protocol, the device operations and the test runner.
- Protocol reference in `docs/protocol.md`.
