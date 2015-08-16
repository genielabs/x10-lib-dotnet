/*
    This file is part of XTenLib source code.

    XTenLib is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTenLib is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTenLib.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/x10-lib-dotnet
 */

using System;
using System.Threading;

using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

namespace XTenLib.Drivers
{
    /// <summary>
    /// CM15 driver.
    /// </summary>
    public class CM15 : XTenInterface, IDisposable
    {
        private UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(0x0BC7, 0x0001);
        //Use the first read endpoint
        //private readonly byte TRANFER_ENDPOINT = UsbConstants.ENDPOINT_DIR_MASK;
        // Number of transfers to sumbit before waiting begins</summary>
        //private readonly int TRANFER_MAX_OUTSTANDING_IO = 3;
        // Number of transfers before terminating the test
        //private readonly int TRANSFER_COUNT = 30;
        // Size of each transfer
        //private int TRANFER_SIZE = 16;

        //private DateTime startTime = DateTime.MinValue;
        private UsbDevice myUsbDevice;

        private UsbEndpointReader reader = null;
        private UsbEndpointWriter writer = null;

        /// <summary>
        /// Releases all resource used by the <see cref="XTenLib.Drivers.CM15"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="XTenLib.Drivers.CM15"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="XTenLib.Drivers.CM15"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the <see cref="XTenLib.Drivers.CM15"/> so
        /// the garbage collector can reclaim the memory that the <see cref="XTenLib.Drivers.CM15"/> was occupying.</remarks>
        public void Dispose()
        {
            if (myUsbDevice != null && myUsbDevice.IsOpen)
            {
                try
                {
                    reader.Abort();
                }
                catch (Exception e)
                {
                    XTenManager.logger.Error(e);
                }
                try
                {
                    writer.Abort();
                }
                catch (Exception e)
                {
                    XTenManager.logger.Error(e);
                }
            }
        }

        /// <summary>
        /// Open the hardware interface.
        /// </summary>
        public bool Open()
        {
            bool success = true;
            //
            try
            {
                // Find and open the usb device.
                myUsbDevice = UsbDevice.OpenUsbDevice(MyUsbFinder);
                // If the device is open and ready
                if (myUsbDevice == null)
                    throw new Exception("X10 CM15Pro device not connected.");
                // If this is a "whole" usb device (libusb-win32, linux libusb)
                // it will have an IUsbDevice interface. If not (WinUSB) the 
                // variable will be null indicating this is an interface of a 
                // device.
                IUsbDevice wholeUsbDevice = myUsbDevice as IUsbDevice;
                if (!ReferenceEquals(wholeUsbDevice, null))
                {
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.
                    //
                    // Select config #1
                    wholeUsbDevice.SetConfiguration(1);
                    // Claim interface #0.
                    wholeUsbDevice.ClaimInterface(0);
                }
                // open read endpoint 1.
                reader = myUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                // open write endpoint 2.
                writer = myUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep02);
                // status request
                this.WriteData(new byte[] { 0x8B });
            }
            catch (Exception e)
            {
                XTenManager.logger.Error(e);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Close the hardware interface.
        /// </summary>
        public void Close()
        {
            this.Dispose();
            if (myUsbDevice != null)
            {
                if (myUsbDevice.DriverMode == UsbDevice.DriverModeType.MonoLibUsb)
                {
                    try
                    {
                        myUsbDevice.Close();
                    }
                    catch (Exception e)
                    {
                        XTenManager.logger.Error(e);
                    }
                }
                myUsbDevice = null;
            }
        }

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <returns>The data.</returns>
        public byte[] ReadData()
        {
            ErrorCode ecRead;
            int transferredIn;
            UsbTransfer usbReadTransfer = null;
            byte[] readBuffer;
            //
            readBuffer = new byte[16];
            ecRead = reader.SubmitAsyncTransfer(readBuffer, 0, 8, 1000, out usbReadTransfer);
            if (ecRead != ErrorCode.None)
            {
                throw new Exception("Submit Async Read Failed.");
            }
            WaitHandle.WaitAll(new WaitHandle[] { usbReadTransfer.AsyncWaitHandle }, 1000, false);
            ecRead = usbReadTransfer.Wait(out transferredIn);

            if (!usbReadTransfer.IsCompleted)
            {
                ecRead = reader.SubmitAsyncTransfer(readBuffer, 8, 8, 1000, out usbReadTransfer);
                if (ecRead != ErrorCode.None)
                {
                    throw new Exception("Submit Async Read Failed.");
                }
                WaitHandle.WaitAll(new WaitHandle[] { usbReadTransfer.AsyncWaitHandle }, 1000, false);
            }

            if (!usbReadTransfer.IsCompleted)
                usbReadTransfer.Cancel();
            try
            {
                ecRead = usbReadTransfer.Wait(out transferredIn);
            }
            catch (Exception e)
            {
                XTenManager.logger.Error(e);
            }
            usbReadTransfer.Dispose();

            byte[] readdata = new byte[transferredIn];
            Array.Copy(readBuffer, readdata, transferredIn);

            return readdata;
        }

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <returns><c>true</c>, if data was written, <c>false</c> otherwise.</returns>
        /// <param name="bytesToSend">Bytes to send.</param>
        public bool WriteData(byte[] bytesToSend)
        {
            ErrorCode ecWrite;
            int transferredOut;
            UsbTransfer usbWriteTransfer = null;

            if (myUsbDevice != null)
            {
                ecWrite = writer.SubmitAsyncTransfer(bytesToSend, 0, bytesToSend.Length, 1000, out usbWriteTransfer);
                if (ecWrite != ErrorCode.None)
                {
                    throw new Exception("Submit Async Write Failed.");
                }

                WaitHandle.WaitAll(new WaitHandle[] { usbWriteTransfer.AsyncWaitHandle }, 1000, false);

                if (!usbWriteTransfer.IsCompleted)
                    usbWriteTransfer.Cancel();
                ecWrite = usbWriteTransfer.Wait(out transferredOut);
                usbWriteTransfer.Dispose();
                // TODO: should check if (transferredOut != bytesToSend.Length), and eventually resend?
                return true;
            }
            return false;
        }

        internal static byte[] BuildTransceivedCodesMessage(string csMonitoredCodes)
        {
            ushort transceivedCodes = 0;

            if (csMonitoredCodes.Contains("A"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 14);
            }
            if (csMonitoredCodes.Contains("B"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 6);
            }
            if (csMonitoredCodes.Contains("C"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 10);
            }
            if (csMonitoredCodes.Contains("D"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 2);
            }
            if (csMonitoredCodes.Contains("E"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 9);
            }
            if (csMonitoredCodes.Contains("F"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 1);
            }
            if (csMonitoredCodes.Contains("G"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 13);
            }
            if (csMonitoredCodes.Contains("H"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 5);
            }
            if (csMonitoredCodes.Contains("I"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 15);
            }
            if (csMonitoredCodes.Contains("J"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 7);
            }
            if (csMonitoredCodes.Contains("K"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 11);
            }
            if (csMonitoredCodes.Contains("L"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 3);
            }
            if (csMonitoredCodes.Contains("M"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 8);
            }
            if (csMonitoredCodes.Contains("N"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 0);
            }
            if (csMonitoredCodes.Contains("O"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 12);
            }
            if (csMonitoredCodes.Contains("P"))
            {
                transceivedCodes |= (ushort)Math.Pow(2, 4);
            }

            byte b1 = (byte)(transceivedCodes >> 8);
            byte b2 = (byte)(transceivedCodes);

            //byte[] trcommand = new byte[] { 0xbb, 0xff, 0xff, 0x05, 0x00, 0x14, 0x20, 0x28, 0x24, 0x29 }; // transceive all
            //byte[] trcommand = new byte[] { 0xbb, 0x40, 0x00, 0x05, 0x00, 0x14, 0x20, 0x28, 0x24, 0x29 }; // autodetect
            byte[] trCommand = new byte[] { 0xbb, b1, b2, 0x05, 0x00, 0x14, 0x20, 0x28, 0x24, 0x29 };

            return trCommand;
        }
    }
}

