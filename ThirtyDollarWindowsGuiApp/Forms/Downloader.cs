using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ThirtyDollarConverter;

namespace ThirtyDollarWindowsGuiApp.Forms
{
    public partial class Downloader : Form
    {
        private bool Downloading = false;
        public Downloader()
        {
            InitializeComponent();
        }

        private async void downloadButton_Click(object sender, EventArgs e)
        {
            var holder = Program.SampleHolder;
            holder.DownloadUpdate = (sound, index, count) =>
            {
                downloadBarLabel.Text = $"Downloading: ({index + 1}) - ({count})";
                downloadBar.Value = index;
                downloadBar.Maximum = count;
                downloadBoxLog.Text += $"Downloading: ({index + 1}) - ({count}): \"{sound}\"{Environment.NewLine}";
                downloadBoxLog.SelectionStart = downloadBoxLog.Text.Length;
                downloadBoxLog.ScrollToCaret();
            };
            Downloading = true;
            await holder.DownloadFiles();
            Downloading = false;
            MessageBox.Show("All samples have been downloaded.", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private void Downloader_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Downloading) e.Cancel = true;
        }
    }
}
