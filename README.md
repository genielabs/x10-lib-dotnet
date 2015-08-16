# X10 Home Automation library for .NET

## Features

- Supports both **CM11** and **CM15** hardware
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

## Example usage

    using XTenLib;
    ...

    var x10 = new XTenManager();

    // Listen to XTenManager events
    x10.ModuleChanged += delegate(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
        var module = sender as X10Module;
        Console.WriteLine("Module property changed: {0} {1}", module.Code, e.PropertyName);
    };
    // This event is only used for CM15
    x10.RfDataReceived += delegate(RfDataReceivedAction obj) {
        Console.WriteLine("RF data received: {0}", BitConverter.ToString(obj.RawData));
    };

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

## License

XTenLib is open source software, licensed under the terms of GNU GPLV3 license. See the [LICENSE](LICENSE) file for details.
