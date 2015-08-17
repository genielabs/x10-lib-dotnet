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

namespace XTenLib
{

    /// <summary>
    /// Connected state changed event arguments.
    /// </summary>
    public class ConnectionStatusChangedEventArgs
    {
        /// <summary>
        /// The connected state.
        /// </summary>
        public readonly bool Connected;

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.ConnectionStatusChangedEventArgs"/> class.
        /// </summary>
        /// <param name="state">State of the connection (true = connected, false = not connected).</param>
        public ConnectionStatusChangedEventArgs(bool state)
        {
            Connected = state;
        }
    }

    /// <summary>
    /// Plc address received event arguments.
    /// </summary>
    public class PlcAddressReceivedEventArgs
    {
        /// <summary>
        /// The house code.
        /// </summary>
        public readonly X10HouseCode HouseCode;
        /// <summary>
        /// The unit code.
        /// </summary>
        public readonly X10UnitCode UnitCode;
        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.PlcAddressReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="hc">Hc.</param>
        /// <param name="uc">Uc.</param>
        public PlcAddressReceivedEventArgs(X10HouseCode hc, X10UnitCode uc)
        {
            HouseCode = hc;
            UnitCode = uc;
        }
    }

    /// <summary>
    /// Plc function received event arguments.
    /// </summary>
    public class PlcFunctionReceivedEventArgs
    {
        /// <summary>
        /// The command.
        /// </summary>
        public readonly X10Command Command;
        /// <summary>
        /// The house code.
        /// </summary>
        public readonly X10HouseCode HouseCode;
        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.PlcFunctionReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="cmd">Cmd.</param>
        /// <param name="hc">Hc.</param>
        public PlcFunctionReceivedEventArgs(X10Command cmd, X10HouseCode hc)
        {
            Command = cmd;
            HouseCode = hc;
        }
    }

    /// <summary>
    /// RF data received event arguments.
    /// </summary>
    public class RfDataReceivedEventArgs
    {
        /// <summary>
        /// The raw data.
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.RfDataReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="data">Data.</param>
        public RfDataReceivedEventArgs(byte[] data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// RF command received event arguments.
    /// </summary>
    public class RfCommandReceivedEventArgs
    {
        /// <summary>
        /// The command.
        /// </summary>
        public readonly X10RfFunction Command;
        /// <summary>
        /// The house code.
        /// </summary>
        public readonly X10HouseCode HouseCode;
        /// <summary>
        /// The unit code.
        /// </summary>
        public readonly X10UnitCode UnitCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.RfCommandReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="function">Function.</param>
        /// <param name="housecode">Housecode.</param>
        /// <param name="unitcode">Unitcode.</param>
        public RfCommandReceivedEventArgs(X10RfFunction function, X10HouseCode housecode, X10UnitCode unitcode)
        {
            Command = function;
            HouseCode = housecode;
            UnitCode = unitcode;
        }
    }

    /// <summary>
    /// RF security received event arguments.
    /// </summary>
    public class RfSecurityReceivedEventArgs
    {
        /// <summary>
        /// The event.
        /// </summary>
        public readonly X10RfSecurityEvent Event;
        /// <summary>
        /// The address.
        /// </summary>
        public readonly uint Address;

        /// <summary>
        /// Initializes a new instance of the <see cref="XTenLib.RfSecurityReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="evt">Evt.</param>
        /// <param name="addr">Address.</param>
        public RfSecurityReceivedEventArgs(X10RfSecurityEvent evt, uint addr)
        {
            Event = evt;
            Address = addr;
        }
    }

}

