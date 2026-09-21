# DTM Toolbox for BLE: quick start

## Before the first test

1. Connect the device under test to the computer through its serial port: a USB-to-serial adapter or the bridge on the board. The device must run a Direct Test Mode firmware with the 2-wire UART interface. Usual link settings: 19200 baud, 8 data bits, no parity, 1 stop bit, no flow control.
2. Start DTM Toolbox for BLE and pick the port in the box at the top left. The list holds every serial port Windows knows, under the name Device Manager shows. It refreshes when a port is plugged or unplugged. "Refresh ports" reads it again.
3. Choose the baud rate of the firmware.
4. Click "Identify device". The application resets the device and reads the features it supports. An answer here means port, baud rate and wiring are right. The result also goes to the ABOUT tab.

## Transmitter test

1. Open the TRANSMITTER tab.
2. Choose "Single" for one channel or "Sweep" for a range of channels with a time per channel.
3. Set the channel. The number is the link layer channel index, the same one the chart shows on top. The frequency is written under it.
4. Set the transmit power, the physical layer, the packet type and the packet length. "Constant carrier" sends an unmodulated carrier. It exists with the Nordic nRF5x vendor extensions, on LE 1M and LE 2M.
5. Optional: set a timeout. At 0 the test runs until it is stopped.
6. Click "Start test". The bar in the chart marks the active channel at the transmit power the device applied, which can differ from the request when the radio does not have that level. "Stop test" ends the test on the device.

## Receiver test

1. Open the RECEIVER tab and set the channel, or the sweep range, and the physical layer.
2. Click "Start test" and start the transmitter of the test equipment.
3. Click "Stop test". On a single channel the test runs without interruption and the packet count arrives when it is stopped. In a sweep the counts add up per channel while it runs.

## Log

Every step is written to the log at the bottom. "RAW FRAMES" adds the bytes of each command and of each answer. Lines are selected like files in a list, and Ctrl+C copies them. "COPY LOG" copies everything and "SAVE LOG" writes a text file.

## When something does not work

| What happens | What to check |
|---|---|
| "No response from the device" | The port is the right one and is not open in another program. Baud rate. TX and RX not swapped. The firmware is a DTM firmware and the board is powered. A power cycle of the board. |
| "Cannot open COMx" | Another program holds the port. Close it and try again. |
| "The device rejected ..." with a transmit power command | Firmware older than Bluetooth 5.2 has no transmit power command. With Nordic nRF5x firmware, keep "Nordic nRF5x" selected under VENDOR EXTENSIONS in the ABOUT tab: the application then uses the vendor command. |
| The port is not in the list | The driver of the USB-to-serial adapter is not installed. The port has to appear in Device Manager under "Ports (COM & LPT)". |
| Windows shows "Windows protected your PC" when Setup starts | The installer is not digitally signed. Click "More info" and then "Run anyway". |

## Files the application writes

- Settings: `%APPDATA%\DtmToolbox\settings.xml`
- Unexpected errors: `%APPDATA%\DtmToolbox\crash.log`
