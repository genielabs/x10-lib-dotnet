/*
    This file is part of XTenLib source code.

    HomeGenie is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    HomeGenie is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with HomeGenie.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/x10-lib-dotnet
 */

#pragma warning disable 1591

namespace XTenLib
{
    public enum X10Command
    {
        All_Units_Off,
        All_Lights_On,
        On,
        Off,
        Dim,
        Bright,
        All_Lights_Off,
        Extended_Code,
        Hail_Request,
        Hail_Acknowledge,
        Preset_Dim_1,
        Preset_Dim_2,
        Extended_Data_transfer,
        Status_On,
        Status_Off,
        Status_Request
    }

    public enum X10FunctionType
    {
        Address = 0x00,
        Function = 0x01
    }

    public enum X10CommState
    {
        Ready,
        WaitingChecksum,
        WaitingAck,
        WaitingPollReply
    }

    public enum X10CommandType
    {
        Address = 0x04,
        Function = 0x06,
        //
        PLC_Ready = 0x55,
        PLC_Poll = 0x5A,
        PLC_FilterFail_Poll = 0xF3,
        // CP10-CM11
        Macro = 0x5B,
        RF = 0x5D,
        //
        PLC_TimeRequest = 0xA5,
        PLC_ReplyToPoll = 0xC3
    }

    public enum X10HouseCode
    {
        A = 6,
        B = 14,
        C = 2,
        D = 10,
        E = 1,
        F = 9,
        G = 5,
        H = 13,
        I = 7,
        J = 15,
        K = 3,
        L = 11,
        M = 0,
        N = 8,
        O = 4,
        P = 12
    }

    public enum X10UnitCode
    {
        Unit_1 = 6,
        Unit_2 = 14,
        Unit_3 = 2,
        Unit_4 = 10,
        Unit_5 = 1,
        Unit_6 = 9,
        Unit_7 = 5,
        Unit_8 = 13,
        Unit_9 = 7,
        Unit_10 = 15,
        Unit_11 = 3,
        Unit_12 = 11,
        Unit_13 = 0,
        Unit_14 = 8,
        Unit_15 = 4,
        Unit_16 = 12
    }
}