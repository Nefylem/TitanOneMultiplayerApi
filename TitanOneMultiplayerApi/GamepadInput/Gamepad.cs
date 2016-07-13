using System;
using System.Collections.Generic;
using TitanOneMultiplayerApi.Properties;
using TitanOneMultiplayerApi.Configuration;
using TitanOneMultiplayerApi.Remapping;
using TitanOneMultiplayerApi.TitanOneOutput;

namespace TitanOneMultiplayerApi.GamepadInput
{
    internal static class Gamepad
    {
        private static int _xboxButtonCount;        
        public static List<TitanOne.GcmapiStatus[]> ReturnOutput;

        public class GamepadOutput
        {
            public int Index;
            public byte[] Output;
            public PlayerIndex PlayerIndex;
        }

        static Gamepad()
        {
            if (_xboxButtonCount == 0) _xboxButtonCount = Enum.GetNames(typeof(Xbox)).Length;

            for (var count = 0; count < 5; count++)
            {
                ReturnOutput[count] = new TitanOne.GcmapiStatus[_xboxButtonCount];
            }
        }

        public static GamepadOutput Check(int index)
        {
            //Technically, you could just say count is 36. I did this so I could add extra buttons if I needed
            if (_xboxButtonCount == 0) _xboxButtonCount = Enum.GetNames(typeof(Xbox)).Length;
            var output = new byte[_xboxButtonCount];

            //Pass back in the information from the controller 
            if (ReturnOutput != null)
            {
                for (var count = 0; count < ReturnOutput[index - 1].Length; count++)
                {
                    output[count] = ReturnOutput[index - 1][count].Value;
                }
            }
        

            var player = FindPlayerIndex(index);                            
            var controls = GamePad.GetState(player);

            if (controls.DPad.Left) { output[GamepadMap.Left] = Convert.ToByte(100); }
            if (controls.DPad.Right) { output[GamepadMap.Right] = Convert.ToByte(100); }
            if (controls.DPad.Up) { output[GamepadMap.Up] = Convert.ToByte(100); }
            if (controls.DPad.Down) { output[GamepadMap.Down] = Convert.ToByte(100); }

            if (controls.Buttons.A) output[GamepadMap.A] = Convert.ToByte(100);
            if (controls.Buttons.B) output[GamepadMap.B] = Convert.ToByte(100);
            if (controls.Buttons.X) output[GamepadMap.X] = Convert.ToByte(100);
            if (controls.Buttons.Y) output[GamepadMap.Y] = Convert.ToByte(100);

            if (controls.Buttons.LeftShoulder) { output[GamepadMap.LeftShoulder] = Convert.ToByte(100); }
            if (controls.Buttons.RightShoulder) { output[GamepadMap.RightShoulder] = Convert.ToByte(100); }
            if (controls.Buttons.LeftStick) { output[GamepadMap.LeftStick] = Convert.ToByte(100); }
            if (controls.Buttons.RightStick) { output[GamepadMap.RightStick] = Convert.ToByte(100); }

            if (controls.Triggers.Left > 0) { output[GamepadMap.LeftTrigger] = Convert.ToByte(controls.Triggers.Left * 100); }
            if (controls.Triggers.Right > 0) { output[GamepadMap.RightTrigger] = Convert.ToByte(controls.Triggers.Right * 100); }

            var leftX = controls.ThumbSticks.Left.X * 100.0;
            var leftY = controls.ThumbSticks.Left.Y * 100.0;
            var rightX = controls.ThumbSticks.Right.X * 100.0;
            var rightY = controls.ThumbSticks.Right.Y * 100.0;

            if (AppSettings.NormalizeControls)
            {
                NormalGamepad(ref leftX, ref leftY);
                NormalGamepad(ref rightX, ref rightY);
            }
            else
            {
                leftY = -leftY;
                rightY = -rightY;
            }

            //You need an sbyte because you need -100 to +100. A byte can only be positive
            if (Math.Abs(leftX) > 0) { output[GamepadMap.LeftX] = (byte)Convert.ToSByte((int)(leftX)); }
            if (Math.Abs(leftY) > 0) { output[GamepadMap.LeftY] = (byte)Convert.ToSByte((int)(leftY)); }
            if (Math.Abs(rightX) > 0) { output[GamepadMap.RightX] = (byte)Convert.ToSByte((int)(rightX)); }
            if (Math.Abs(rightY) > 0) { output[GamepadMap.RightY] = (byte)Convert.ToSByte((int)(rightY)); }

            if (controls.Buttons.Guide) { output[GamepadMap.Home] = Convert.ToByte(100); }
            if (controls.Buttons.Start) { output[GamepadMap.Start] = Convert.ToByte(100); }
            if (controls.Buttons.Back)
            {
                if (AppSettings.Ds4ControllerMode)
                    output[GamepadMap.Touch] = Convert.ToByte(100);
                else
                    output[GamepadMap.Back] = Convert.ToByte(100);
            }

            return new GamepadOutput()
            {
                Output = output,
                Index = index,
                PlayerIndex = player                    //Store this for returning rumble
            };
        }

        //Output is -100 to 100 in both x and y. Reading from a gamepad is circular, output on angles is more like 75, 75 instead. 
        //This turns a circle into a square basically.
        private static void NormalGamepad(ref double x, ref double y)
        {
            var calcX = x;
            var calcY = y;

            var length = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
            if (length > 99.9)
            {
                var theta = Math.Atan2(y, x);
                var angle = (90 - ((theta * 180) / Math.PI)) % 360;

                if ((angle < 0) && (angle >= -45)) { calcX = (int)(100 / Math.Tan(theta)); calcY = -100; }
                if ((angle >= 0) && (angle <= 45)) { calcX = (int)(100 / Math.Tan(theta)); calcY = -100; }
                if ((angle > 45) && (angle <= 135)) { calcY = -(int)(Math.Tan(theta) * 100); calcX = 100; }
                if ((angle > 135) && (angle <= 225)) { calcX = -(int)(100 / Math.Tan(theta)); calcY = 100; }
                if (angle > 225) { calcY = (int)(Math.Tan(theta) * 100); calcX = -100; }
                if (angle < -45) { calcY = (int)(Math.Tan(theta) * 100); calcX = -100; }
            }
            else
            {
                calcY = -calcY;
            }

            //Return values
            x = calcX;
            y = calcY;
        }

        public static void SetState(int index, double leftMotor, double rightMotor)
        {
            //The xbox one controller has four motors. This should probably get updated at some stage for the extras
            var vibration = new XInputVibration()
            {
                LeftMotorSpeed = (ushort)(65535d * leftMotor),
                RightMotorSpeed = (ushort)(65535d * rightMotor)
            };
            Imports.XInputSetState(index, ref vibration);
        }

        //Fun idea. Get a multiplayer splitscreen game together. Set a timer after 10 minutes to start randomly switching the control order every 30 seconds. 
        private static PlayerIndex FindPlayerIndex(int index)
        {
            switch (index)
            {
                case 1: { return PlayerIndex.One; }
                case 2: { return PlayerIndex.Two; }
                case 3: { return PlayerIndex.Three; }
                case 4: { return PlayerIndex.Four; }
            }
            return PlayerIndex.One;
        }
    }
}
