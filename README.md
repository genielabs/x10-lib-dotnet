[![Build status](https://ci.appveyor.com/api/projects/status/tpg5mnp8a4j2ehgg?svg=true)](https://ci.appveyor.com/project/genemars/x10-lib-dotnet)
[![NuGet](https://img.shields.io/nuget/v/XTenLib.svg)](https://www.nuget.org/packages/XTenLib/)
![License](https://img.shields.io/github/license/genielabs/x10-lib-dotnet.svg)

# X10 Home Automation library for .NET

## Features

- Supports both **CM11** and **CM15** hardware
- Decoding of CM15 RF messages (both standard and security)
- Event driven
- Hot plug
- Automatically restabilish connection on error/disconnect
- Compatible with Mono

## Requirements for using with CM15 interface

### Linux / Mac OSX

Install the libusb-1.0 package

    apt-get install libusb-1.0-0 libusb-1.0-0-dev

### Windows

Install the CM15 LibUSB driver by executing the *InstallDriver.exe* file contained in the *WindowsUsbDriver* folder.

## NuGet Package

XTenLib  is available as a [NuGet package](https://www.nuget.org/packages/XTenLib).

Run `Install-Package XTenLib` in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) or search for “XTenLib” in your IDE’s package management plug-in.

## Example usage

```csharp
using XTenLib;
//...

var x10 = new XTenManager();

// Listen to XTenManager events
x10.ConnectionStatusChanged += X10_ConnectionStatusChanged;
x10.ModuleChanged += X10_ModuleChanged;
x10.PlcAddressReceived += X10_PlcAddressReceived;
x10.PlcFunctionReceived += X10_PlcFunctionReceived;
// These RF events are only used for CM15
x10.RfDataReceived += X10_RfDataReceived;
x10.RfCommandReceived += X10_RfCommandReceived;
x10.RfSecurityReceived += X10_RfSecurityReceived;

// Setup X10 interface. For CM15 set PortName = "USB",
// for CM11 use serial port path instead (eg. "COM7" or "/dev/ttyUSB0")
x10.PortName = "USB";
x10.HouseCode = "A,C";

// Connect the interface. It supports hot plug/unplug.
x10.Connect();

// Get a module and control it
if (x10.IsConnected)
{
    var modC7 = x10.Modules["C7"];
    // Turn On
    modC7.On();
    // Turn Off
    modC7.Off();
    // Brighten by 10%
    modC7.Bright(10);
}

// Disconnect the interface
x10.Disconnect();

//...

void X10_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
{
    Console.WriteLine("Interface connection status {0}", args.Connected);
}

void X10_ModuleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    var module = sender as X10Module;
    Console.WriteLine("Module property changed: {0} {1} = {2}", 
        module.Code, e.PropertyName, module.Level);
}

void X10_PlcAddressReceived(object sender, PlcAddressReceivedEventArgs args)
{
    Console.WriteLine("PLC address received: HouseCode {0} Unit {1}", 
        args.HouseCode, args.UnitCode);
}

void X10_PlcFunctionReceived(object sender, PlcFunctionReceivedEventArgs args)
{
    Console.WriteLine("PLC function received: Command {0} HouseCode {1}", 
        args.Command, args.HouseCode);
}

void X10_RfDataReceived(object sender, RfDataReceivedEventArgs args)
{
    Console.WriteLine("RF data received: {0}", BitConverter.ToString(args.Data));
}

void X10_RfCommandReceived(object sender, RfCommandReceivedEventArgs args)
{
    Console.WriteLine("Received RF command {0} House Code {1} Unit {2}", 
        args.Command, args.HouseCode, args.UnitCode);
}

void X10_RfSecurityReceived(object sender, RfSecurityReceivedEventArgs args)
{
    Console.WriteLine("Received RF Security event {0} from address {1}", 
        args.Event, args.Address.ToString("X3"));
}
```

## License

XTenLib is open source software, licensed under the terms of Apache license 2.0. See the [LICENSE](LICENSE) file for details.
