# Syringe Pump Controller Project

This project provides a complete solution for building a cost-effective syringe pump controller. It includes a C# GUI application for controlling the pump, PCB designs, circuit schematics, 3D printed components, and Arduino code for the ESP32 microcontroller.

## Table of Contents

1. [Introduction](#introduction)
2. [Project Structure](#project-structure)
3. [C# GUI Application](#c-gui-application)
4. [PCB Design](#pcb-design)
5. [Circuit and PCB Files](#circuit-and-pcb-files)
6. [3D Models](#3d-models)
7. [Arduino Code](#arduino-code)
8. [Documentation](#documentation)
9. [How to Contribute](#how-to-contribute)
10. [License](#license)

## Introduction

This project is focused on developing an affordable syringe pump controller that can be assembled with readily available components. The controller is built around an ESP32 microcontroller, with a C# application providing a user-friendly interface. The hardware includes a custom PCB and 3D-printed components to house the electronics and mechanical parts.

## Project Structure

The repository is organized as follows:

- **GUI/**: C# codebase for the graphical user interface (GUI) application.
- **PCB/**: Gerber files for manufacturing the printed circuit board (PCB).
- **circuit_board/**: KiCad project files, including the circuit schematic and PCB layout.
- **models/**: 3D models for printing the syringe pump housing and related parts.
- **syringe_pump_controller.ino**: Arduino sketch for the ESP32 microcontroller, handling motor control and communication with the GUI.
- **docs.pdf**: Comprehensive documentation covering project setup, usage, and technical details.

## C# GUI Application

The C# GUI application provides control over the syringe pump's operation. Key features include:

- Configuring syringe diameter, fluid volume, and flow rate.
- Real-time motor control, including start, stop, and jog functions.
- Monitoring of current syringe position, motor RPM, and estimated time to empty.

The application is developed in C# using .NET, and the source code is located in the `GUI` directory. It can be built using Visual Studio or the .NET CLI.

## PCB Design

The custom PCB integrates the ESP32 microcontroller with the motor driver and other essential components. The Gerber files in the `PCB` directory are ready for submission to any PCB fabrication service.

## Circuit and PCB Files

The `circuit_board` directory contains the KiCad files for the project, including the full circuit schematic and PCB layout. These files are intended for users who need to modify the circuit design or manufacture their own PCBs.

## 3D Models

The `models` directory includes STL files for 3D printing the syringe pump housing and other mechanical parts. These models are designed to fit standard syringe sizes and the custom PCB.

## Arduino Code

The `syringe_pump_controller.ino` file is the Arduino code that runs on the ESP32. This code handles:

- Motor control, including speed, direction, and position tracking.
- Communication with the C# GUI over serial.
- Homing and emergency stop functions.

The code is designed to be flexible and can be modified for different hardware configurations.

## Documentation

Detailed documentation is available in `docs.pdf`, covering all aspects of the project, from hardware assembly to software configuration. It also includes troubleshooting tips, among other things.

## How to Contribute

Contributions are welcome. If you have improvements or fixes, please fork the repository, make your changes, and submit a pull request. Whether it's code enhancements, bug fixes, or documentation updates, your contributions are appreciated.

## License

This project is licensed under the MIT License. See the `LICENSE` file for more details.
