using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TitanOneMultiplayerApi.GamepadInput;
using TitanOneMultiplayerApi.TitanOneOutput;

namespace TitanOneMultiplayerApi
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Debugging.Debug.SetHome(this);
            Gamepad.Setup();
            TitanOne.Open();
            TitanOne.FindDevices();
            timer1.Enabled = true;

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            for (var count = 1; count < 5; count++)
            {
                var input = Gamepad.Check(count);
                var report = TitanOne.Send(input);

                //Display gamepad input
                //Display titanone report
            }
        }

    }
}
