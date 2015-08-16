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
using System.IO;
using System.IO.Ports;

namespace XTenLib.Drivers
{
    /// <summary>
    /// CM11 driver.
    /// </summary>
    public class CM11 : XTenInterface
    {
        private SerialPort serialPort;
        private string portName = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.Drivers.CM11"/> class.
        /// </summary>
        /// <param name="port">Serial port path.</param>
        public CM11(string port)
        {
            portName = port;
        }

        /// <summary>
        /// Open the hardware interface.
        /// </summary>
        public bool Open()
        {
            bool success = false;
            //
            try
            {
                bool tryOpen = (serialPort == null);
                if (Environment.OSVersion.Platform.ToString().StartsWith("Win") == false)
                {
                    tryOpen = (tryOpen && System.IO.File.Exists(portName));
                }
                if (tryOpen)
                {
                    serialPort = new SerialPort();
                    serialPort.PortName = portName;
                    serialPort.BaudRate = 4800;
                    serialPort.Parity = Parity.None;
                    serialPort.DataBits = 8;
                    serialPort.StopBits = StopBits.One;
                    serialPort.ReadTimeout = 150;
                    serialPort.WriteTimeout = 150;
                    //serialPort.RtsEnable = true;

                    // DataReceived event won't work under Linux / Mono
                    //serialPort.DataReceived += HandleDataReceived;
                    //serialPort.ErrorReceived += HanldeErrorReceived;
                }
                if (serialPort.IsOpen == false)
                {
                    serialPort.Open();
                }
                // Send staus request on connection
                this.WriteData(new byte[] { 0x8B });
                success = true;
            }
            catch (Exception e)
            {
                XTenManager.logger.Error(e);
            }

            return success;
        }

        /// <summary>
        /// Close the hardware interface.
        /// </summary>
        public void Close()
        {
            if (serialPort != null)
            {
                //serialPort.DataReceived -= HandleDataReceived
                //serialPort.ErrorReceived -= HanldeErrorReceived;
                try
                {
                    //serialPort.Dispose();
                    serialPort.Close();
                }
                catch (Exception e)
                {
                    XTenManager.logger.Error(e);
                }
                serialPort = null;
            }
        }

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <returns>The data.</returns>
        public byte[] ReadData()
        {
            int buflen = 32;
            int length = 0;
            int readBytes = 0;
            byte[] buffer = new byte[buflen];
            do
            {
                readBytes = serialPort.Read(buffer, length, buflen - length);
                length += readBytes;
                if (length > 1 && buffer[0] < length)
                    break;
                else if (buffer[0] > 0x10 && serialPort.BytesToRead == 0)
                    break;
            } while (readBytes > 0 && (buflen - length > 0));

            byte[] readData = new byte[length + 1];
            if (length > 1 && length < 13)
            {
                readData[0] = (int)X10CommandType.PLC_Poll;
                Array.Copy(buffer, 0, readData, 1, length);
            }
            else
            {
                Array.Copy(buffer, readData, length);
            }

            return readData;
        }

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <returns>true</returns>
        /// <c>false</c>
        /// <param name="bytesToSend">Bytes to send.</param>
        public bool WriteData(byte[] bytesToSend)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(bytesToSend, 0, bytesToSend.Length);
                return true;
            }
            return false;
        }

    }
}

