using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ThirtyDollarWindowsGuiApp.Forms
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Load(object sender, EventArgs e)
        {
            var greeting = new GreetingBox();
            greeting.ShowDialog();
            var holder = Program.SampleHolder;
            await holder.LoadSampleList();
            if (holder.DownloadedAllFiles()) return;
            var downloader = new Downloader();
            downloader.ShowDialog();
        }
    }
}
