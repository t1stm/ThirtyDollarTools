using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ThirtyDollarWindowsGuiApp.Forms
{
    partial class GreetingBox : Form
    {
        public GreetingBox()
        {
            InitializeComponent();
            Text = "Hello! Welcome to the Thirty Dollar Converter!";
        }

        private void GotItButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
