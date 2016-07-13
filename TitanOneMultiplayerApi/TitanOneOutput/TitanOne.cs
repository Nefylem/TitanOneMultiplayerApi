using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using TitanOneMultiplayerApi.Configuration;
using TitanOneMultiplayerApi.Debugging;
using TitanOneMultiplayerApi.GamepadInput;
using TitanOneMultiplayerApi.Properties;

namespace TitanOneMultiplayerApi.TitanOneOutput
{
    //This doesn't need static access. I've simply added it so script from anywhere can call TitanOne.Send(output) without having to either find its definition or recreate it.
    internal static class TitanOne
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        public struct GcmapiConstants
        {
            public const int GcapiInputTotal = 30;
            public const int GcapiOutputTotal = 36;
        }

        public struct GcmapiStatus
        {
            public byte Value; // Current value - Range: [-100 ~ 100] %
            public byte PrevValue; // Previous value - Range: [-100 ~ 100] %
            public int PressTv; // Time marker for the button press event
        }

        public struct Report
        {
            public byte Console; // Receives values established by the #defines CONSOLE_*
            public byte Controller; // Values from #defines CONTROLLER_* and EXTENSION_*

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Led; // Four LED - #defines LED_*

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] //XBOX ONE TRIGGER RUMBLE
            public byte[] Rumble; // Two rumbles - Range: [0 ~ 100] %

            public byte BatteryLevel; // Battery level - Range: [0 ~ 10] 0 = empty, 10 = full

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = GcmapiConstants.GcapiInputTotal,
                ArraySubType = UnmanagedType.Struct)]
            public GcmapiStatus[] Input;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate byte GcmapiLoad();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void GcmapiUnload();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GcmapiConnect(ushort devPid);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GcmapiGetserialnumber(int devId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GcmapiIsconnected(int m);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GcmapiWrite(int device, byte[] output);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GcmapiRead(int device, [In, Out] ref Report report);

        public static GcmapiLoad Load;
        public static GcmapiUnload Unload;
        public static GcmapiConnect Connect;
        public static GcmapiGetserialnumber Serial;
        public static GcmapiIsconnected Connected;
        public static GcmapiWrite Write;
        public static GcmapiRead Read;

        private static List<int> _notConnected;
        private static List<int> _connected;

        public static List<TitanDevices> DeviceList;

        public class TitanDevices
        {
            public int Id;
            public string SerialNo;
        }

        //Connect to our DLL for the base functions
        public static void Open()
        {
            var file = "gcdapi.dll";
            if (string.IsNullOrEmpty(file))
            {
                Debug.Log("Error loading TitanOne gcdapi.dll");
                return;
            }

            var dll = LoadLibrary(file);
            if (dll == IntPtr.Zero)
            {
                Debug.Log("[FAIL] Unable to allocate Device API");
                return;
            }

            var load = LoadExternalFunction(dll, "gcmapi_Load");
            if (load == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcMapi_Load");
                return;
            }

            var unload = LoadExternalFunction(dll, "gcmapi_Unload");
            if (unload == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcMapi_Unload");
                return;
            }

            var connect = LoadExternalFunction(dll, "gcmapi_Connect");
            if (connect == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcmapi_Connect");
                return;
            }

            var connected = LoadExternalFunction(dll, "gcmapi_IsConnected");
            if (connected == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcmapi_IsConnected");
                return;
            }

            var serial = LoadExternalFunction(dll, "gcmapi_GetSerialNumber");
            if (serial == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcmapi_GetSerialNumber");
                return;
            }

            var write = LoadExternalFunction(dll, "gcmapi_Write");
            if (write == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcmapi_Write");
                return;
            }

            var read = LoadExternalFunction(dll, "gcmapi_Read");
            if (read == IntPtr.Zero)
            {
                Debug.Log("[FAIL] gcmapi_Read");
                return;
            }

            try
            {
                Load = (GcmapiLoad)Marshal.GetDelegateForFunctionPointer(load, typeof(GcmapiLoad));
                Unload = (GcmapiUnload)Marshal.GetDelegateForFunctionPointer(unload, typeof(GcmapiUnload));
                Connect = (GcmapiConnect)Marshal.GetDelegateForFunctionPointer(connect, typeof(GcmapiConnect));
                Serial = (GcmapiGetserialnumber)Marshal.GetDelegateForFunctionPointer(serial, typeof(GcmapiGetserialnumber));
                Write = (GcmapiWrite)Marshal.GetDelegateForFunctionPointer(write, typeof(GcmapiWrite));
                Connected = (GcmapiIsconnected)Marshal.GetDelegateForFunctionPointer(connected, typeof(GcmapiIsconnected));
                Read = (GcmapiRead)Marshal.GetDelegateForFunctionPointer(read, typeof(GcmapiRead));
            }
            catch (Exception ex)
            {
                Debug.Log("Fail -> " + ex);
                Debug.Log("[ERR] Critical failure loading TitanOne API.");
                return;
            }

            Debug.Log("TitanOne API initialised ok");
        }

        private static IntPtr LoadExternalFunction(IntPtr dll, string function)
        {
            var ptr = GetProcAddress(dll, function);
            Debug.Log(ptr == IntPtr.Zero ? $"[NG] {function} alloc fail" : $"[OK] {function}");
            return ptr;
        }

        public static void FindDevices()
        {
            DeviceList = new List<TitanDevices>();
            if (Connect == null) return;            //If the API hasn't been loaded

            var deviceCount = Load();
            Debug.Log($"Number of devices found: {deviceCount}");
            Connect(0x003);                         //TitanOne device moniker

            for (var count = 0; count <= deviceCount; count++)
            {
                if (Connected(count) == 0) continue;
                var serial = ReadSerial(count);

                Debug.Log($"Device found: [ID]{count} [SERIAL]{serial}");

                DeviceList.Add(new TitanDevices()
                {
                    Id = count,
                    SerialNo = serial
                });
            }
        }

        private static string ReadSerial(int devId)
        {
            var serial = new byte[20];
            var ret = Serial(devId);
            Marshal.Copy(ret, serial, 0, 20);
            var serialNo = "";
            foreach (var item in serial)
            {
                serialNo += $"{item:X2}";
            }
            return serialNo;
        }

        public static GcmapiStatus[] Send(Gamepad.GamepadOutput player)
        {
            if (_notConnected == null) _notConnected = new List<int>();
            if (_connected == null) _connected = new List<int>();

            //Do a nice little notifier to know if the device is found or not
            if (Connected(player.Index) != 1)
            {
                if (_notConnected.IndexOf(player.Index) > -1) return null;
                if (_connected.IndexOf(player.Index) > -1) _connected.Remove(player.Index);
                _notConnected.Add(player.Index);
                Debug.Log($"TitanOne device {player.Index} not connected");
                return null;
            }

            if (_connected.IndexOf(player.Index) == -1)
            {
                _connected.Add(player.Index);
                if (_notConnected.IndexOf(player.Index) > -1) _notConnected.Remove(player.Index);
                Debug.Log($"TitanOne device {player.Index} connected");
            }

            Write(player.Index, player.Output);

            var report = new Report();
            if (Read(player.Index, ref report) == IntPtr.Zero) return null;
            if (AppSettings.AllowPassthrough) Gamepad.ReturnOutput[player.Index - 1] = report.Input;
            if (AppSettings.AllowRumble[player.Index]) Gamepad.SetState(player.Index, report.Rumble[0], report.Rumble[1]);
            return report.Input;
        }

        /*
         * Untested information - courtesy of J2KBR. Will experiment with this before putting it into code. Including as notes in case anyone wants to pick it up
         * Block authenticating device rumble
         * 
         * it is gcapi_WriteEX(uint8_t *outpacket, uint8_t size)

the outpacket has this format:
CODE: SELECT ALL
[0xFF,0x01 : 2 byte, Packet Signature]
    [Update LED Command (0,1) : 1 byte]
        [LED 1 Status : 1 byte]
        [LED 2 Status : 1 byte]
        [LED 3 Status : 1 byte]
        [LED 4 Status : 1 byte]
    [Reset LEDs Command (0,1) : 1 byte]
    [Update Rumble Command (0,1) : 1 byte]
        [Rumble 1 Value : 1 byte]
        [Rumble 2 Value : 1 byte]
        [Rumble 3 Value : 1 byte]
        [Rumble 4 Value : 1 byte]
    [Reset Rumble Command (0,1) : 1 byte]
    [Block Rumble Command (0,1) : 1 byte]
    [Turn Off Controller Command (0,1) : 1 byte]
    [Button States : 36 bytes - same format as gcapi_Write]

With gcapi_WriteEX you can "block" the rumble by setting the "Block Rumble Command" to 1. You just need send one time ... subsequent gcapi_WriteEX should have 0 on the command byte.
         */

    }
}
