using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ThirtyDollarConverter;
using ThirtyDollarParser;

namespace ThirtyDollarWindowsGuiApp.Forms
{
    public partial class MainWindow : Form
    {
        private bool _running = false;


        public MainWindow()
        {
            InitializeComponent();
            logBox.Text = $"Hello! Welcome to the Thirty Dollar Converter. {Environment.NewLine}" +
                "Please select a sequence and a save location before starting the conversion.";
        }

        private async void MainWindow_Load(object sender, EventArgs e)
        {
            using var greeting = new GreetingBox();
            greeting.ShowDialog();
            var holder = Program.SampleHolder;
            await holder.LoadSampleList();
            if (holder.DownloadedAllFiles()) 
            {
                holder.LoadSamplesIntoMemory();
                return;
            };
            var downloader = new Downloader();
            downloader.ShowDialog();
            downloader.Dispose();
            while (!holder.DownloadedAllFiles())
            {
                var result = MessageBox.Show("Not all samples have been downloaded. Please try downloading them again.", "Error", 
                    MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                switch (result) 
                {
                    case DialogResult.Retry:
                        downloader = new Downloader();
                        downloader.ShowDialog();
                        downloader.Dispose();
                        continue;

                    case DialogResult.Cancel:
                        MessageBox.Show("The program will continue running, but you can expect it to crash if a sample is missing when requested.");
                        return;
                }
            }
            holder.LoadSamplesIntoMemory();
        }

        private void sequenceLocationButton_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Thirty Dollar Sequence Files (*.🗿)|*.🗿|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.InitialDirectory = ".";
            dialog.Title = "Select a sequence file.";
            dialog.DefaultExt = "*.🗿";
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                sequenceLocationBox.Text = dialog.FileName;
            }
        }

        private void saveLocationButton_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "Wave File (*.wav)|*.wav";
            dialog.InitialDirectory = ".";
            dialog.CheckPathExists = true;
            dialog.CheckWriteAccess = true;
            dialog.Title = "Choose where to save the audio file.";
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                saveLocationBox.Text = dialog.FileName;
            }
        }

        private void startConvertingButton_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                MessageBox.Show("An encode is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _running = true;
            var sequenceLocation = sequenceLocationBox.Text;
            Composition composition;
            if (!File.Exists(sequenceLocation))
            {
                MessageBox.Show("Selected sequence doesn't exist on the disk.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _running = false;
                return;
            }
            if (string.IsNullOrEmpty(saveLocationBox.Text)) 
            {
                MessageBox.Show("The save location isn't set. Please set it.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _running = false;
                return;
            }
            try
            {
                var data = File.ReadAllText(sequenceLocation);
                composition = Composition.FromString(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _running = false;
                throw;
            }
            var buffer = "";
            var val = 0ul;
            var max = 100ul;
            var indexAction = new Action<ulong, ulong>((index, count) =>
            {
                val = index;
                max = count;
            });
            var logAction = new Action<string>(str =>
            {
                buffer += str + Environment.NewLine;
            });
            logBox.Text = "Starting encode." + Environment.NewLine;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var task = new Task(() =>
            {
                try
                {
                    var encoder = new PcmEncoder(Program.SampleHolder, composition, new EncoderSettings()
                    {
                        Channels = 2,
                        SampleRate = 48000
                    }, logCheckbox.Checked ? logAction : null, indexAction);
                    var sampled = encoder.SampleComposition(encoder.Composition);
                    encoder.WriteAsWavFile(saveLocationBox.Text, sampled);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "An exception occured. This is usually bad.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _running = false;
                }
            });
            task.Start();
            while (_running)
            {
                Task.Delay(66).Wait();
                progressBar.Value = Math.Clamp((int) val, 0, progressBar.Maximum);
                progressBar.Maximum = Math.Clamp((int) max, progressBar.Value, int.MaxValue);
                if (string.IsNullOrEmpty(buffer)) continue;
                logBox.Text += Environment.NewLine + buffer;
                buffer = "";
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
            }
            stopwatch.Stop();
            logBox.Text += Environment.NewLine + $"Encoding Finished. Took: {stopwatch.Elapsed:c}";
            logBox.SelectionStart = logBox.Text.Length;
            logBox.ScrollToCaret();

            progressBar.Value = 0;
            progressBar.Maximum = 100;
        }

        private void previewSequenceButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Unfortunately, this doesn't do anything at the moment. Sorry.", "Not implemented.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
