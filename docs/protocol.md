# 2-wire UART protocol as implemented

Reference: Bluetooth Core Specification, Vol 6, Part F (Direct Test Mode), UART test interface.

## Link

- 8 data bits, no parity, 1 stop bit, no flow control. Default 19200 baud.
- Every command is 2 bytes, most significant byte first. The device answers every command with a 2-byte event.
- Both bytes of a command go out in one write. A device discards a command whose second byte arrives more than 5 ms after the first.
- The input buffer is cleared before each command. Response timeout: 500 ms.

## Command frame

| Bits | Setup and end commands | Receiver and transmitter commands |
|---|---|---|
| 15-14 | Command code | Command code |
| 13-8 | Control | RF channel, 0 to 39 |
| 7-2 | Parameter (bits 7-0 are one 8-bit field) | Payload length, 6 least significant bits |
| 1-0 | | Packet type |

Command codes: `00` test setup, `01` receiver test, `10` transmitter test, `11` test end.

RF channel `n` is `2402 + 2n` MHz. Link layer channel indexes 37, 38 and 39 are RF channels 0, 12 and 39.

## Setup commands

| Control | Operation | Parameter | Response |
|---|---|---|---|
| `0x00` | Reset | `0x00` | |
| `0x01` | Upper length bits | `(length >> 6) << 2` | |
| `0x02` | PHY | `0x04` LE 1M, `0x08` LE 2M, `0x0C` LE Coded S=8, `0x10` LE Coded S=2 | |
| `0x03` | Modulation index | `0x00` standard, `0x04` stable | |
| `0x04` | Read features | `0x00` | Feature bits |
| `0x05` | Read supported maximum | `0x00` TX octets, `0x04` TX time, `0x08` RX octets, `0x0C` RX time, `0x10` CTE length | Value; times in units of 2 µs |
| `0x09` | Transmit power | Level in dBm as a signed 8-bit value, -127 to +20; `0x7E` device minimum, `0x7F` device maximum | Level applied |

Constant tone extension and antenna commands (`0x06` to `0x08`) are not implemented.

## Packet types

| Code | Payload |
|---|---|
| `00` | PRBS9 |
| `01` | 11110000 |
| `10` | 10101010 |
| `11` | 11111111 on the LE Coded PHY; vendor command on LE 1M and LE 2M |

## Events

| Bit 15 | Event | Content |
|---|---|---|
| 0 | Status | Bit 0: `0` success, `1` error. Bits 14-1: response of the setup command |
| 1 | Packet report | Bits 14-0: packets received during the test that was ended |

Feature bits in the response field, from bit 0: data length extension, LE 2M PHY, stable modulation index, LE Coded PHY, constant tone extension, antenna switching, AoD 1 µs transmission, AoD 1 µs reception, AoA 1 µs reception.

Transmit power response field: bits 7-0 level applied in dBm (signed), bit 8 device minimum reached, bit 9 device maximum reached. A device without the requested level applies the nearest one it supports.

## Test sequence

1. Reset.
2. Transmitter only: transmit power, then upper length bits.
3. Modulation index, then PHY.
4. Receiver or transmitter test command on the channel.
5. Test end. After a receiver test the packet report carries the count.

A sweep repeats steps 4 and 5 on each channel of the range, staying on a channel for the dwell time, and starts over after the last channel. Packet counts add up per channel.

A receiver test on a single channel runs as one uninterrupted test until it is stopped. Restarting it to refresh the count would lose the packets sent during each restart, which matters when the count is compared with a fixed number of transmitted packets.

## Vendor commands

Nordic Semiconductor nRF5x DTM firmware, on LE 1M or LE 2M: a transmitter test command with packet type `11` is a vendor command. The length field selects the command and the channel field carries its argument.

| Length field | Command | Channel field |
|---|---|---|
| `0` | Constant carrier, unmodulated, until test end | RF channel |
