/*
  This file is part of XTenLib (https://github.com/genielabs/x10-lib-dotnet)
 
  Copyright (2012-2018) G-Labs (https://github.com/genielabs)

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: https://github.com/genielabs/x10-lib-dotnet
 */

#pragma warning disable 1591

namespace XTenLib
{
    public enum X10Defs
    {
        RfCommandPrefix = 0x20,
        RfSecurityPrefix = 0x29,
        DimBrightStep = 0x0F
    }

    public enum X10Command
    {
        All_Units_Off,
        All_Lights_On,
        On,
        Off,
        Dim,
        Bright,
        Extended_Code,
        Hail_Request,
        Hail_Acknowledge,
        Preset_Dim_1,
        Preset_Dim_2,
        Extended_Data_transfer,
        Status_On,
        Status_Off,
        Status_Request,
        NotSet
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
        NotSet = 0xFF,
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
        Unit_NotSet = 0xFF,
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

    public enum X10RfFunction
    {
        NotSet = 0xFF,
        On = 0x00,
        Off = 0x01,
        AllLightsOn = 0x90,
        AllUnitsOff = 0x80,
        Dim = 0x98,
        Bright = 0x88
    }

    public enum X10RfSecurityEvent
    {
        NotSet = 0xFF,

        Motion_Alert = 0x0C,
        Motion_Normal = 0x8C,

        Motion_BatteryLow = 0x0D,
        Motion_BatteryOk = 0x8D,

        DoorSensor1_Alert = 0x04,
        DoorSensor1_Normal = 0x84,
        DoorSensor1_Alert_Tarmper = 0x44,
        DoorSensor1_Normal_Tamper = 0xC4,

        DoorSensor2_Alert = 0x00,
        DoorSensor2_Normal = 0x80,
        DoorSensor2_Alert_Tamper = 0x40,
        DoorSensor2_Normal_Tamper = 0xC0,

        DoorSensor1_BatteryLow = 0x01,
        DoorSensor1_BatteryOk = 0x81,
        DoorSensor2_BatteryLow = 0x05,
        DoorSensor2_BatteryOk = 0x85,

        Remote_ArmAway = 0x06,
        Remote_ArmHome = 0x0E,
        Remote_Disarm = 0x86,
        Remote_LightOn = 0x46,
        Remote_LightOff = 0xC6,
        Remote_Panic = 0x26,
        Remote_Panic_15 = 0x03
    }

    public static class X10UnitCodeExt
    {
        public static int Value(this X10UnitCode uc)
        {
            var parts = uc.ToString().Split('_');
            var unitCode = 0;
            int.TryParse(parts[1], out unitCode);
            return unitCode;
        }
    }
}

#pragma warning restore 1591
