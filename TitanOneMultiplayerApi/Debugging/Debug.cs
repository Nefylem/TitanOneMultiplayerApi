using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TitanOneMultiplayerApi.Debugging
{
    internal static class Debug
    {
        private static ListBox _list;
        public static void SetHome(Form1 form1)
        {
            _list = form1.listBox1;
        }

        public static void Log(string write)
        {
            _list.Items.Add(write);
            var items = (int)(_list.Height / _list.ItemHeight);
            _list.TopIndex = _list.Items.Count - items;
        }
    }
}
