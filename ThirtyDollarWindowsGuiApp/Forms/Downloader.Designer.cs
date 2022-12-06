namespace ThirtyDollarWindowsGuiApp.Forms
{
    partial class Downloader
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Downloader));
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.downloadBoxLog = new System.Windows.Forms.TextBox();
            this.downloadBar = new System.Windows.Forms.ProgressBar();
            this.downloadBarLabel = new System.Windows.Forms.Label();
            this.downloadButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox1.Location = new System.Drawing.Point(12, 12);
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ShortcutsEnabled = false;
            this.textBox1.Size = new System.Drawing.Size(360, 26);
            this.textBox1.TabIndex = 0;
            this.textBox1.Text = "First Time Setup.";
            this.textBox1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBox2
            // 
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox2.Location = new System.Drawing.Point(12, 44);
            this.textBox2.Multiline = true;
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(360, 69);
            this.textBox2.TabIndex = 1;
            this.textBox2.Text = resources.GetString("textBox2.Text");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 116);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(87, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Download Log:";
            // 
            // downloadBoxLog
            // 
            this.downloadBoxLog.BackColor = System.Drawing.SystemColors.Window;
            this.downloadBoxLog.Location = new System.Drawing.Point(12, 134);
            this.downloadBoxLog.Multiline = true;
            this.downloadBoxLog.Name = "downloadBoxLog";
            this.downloadBoxLog.ReadOnly = true;
            this.downloadBoxLog.ShortcutsEnabled = false;
            this.downloadBoxLog.Size = new System.Drawing.Size(360, 133);
            this.downloadBoxLog.TabIndex = 3;
            // 
            // downloadBar
            // 
            this.downloadBar.Location = new System.Drawing.Point(12, 288);
            this.downloadBar.Name = "downloadBar";
            this.downloadBar.Size = new System.Drawing.Size(360, 23);
            this.downloadBar.TabIndex = 4;
            // 
            // downloadBarLabel
            // 
            this.downloadBarLabel.AutoSize = true;
            this.downloadBarLabel.Location = new System.Drawing.Point(12, 270);
            this.downloadBarLabel.Name = "downloadBarLabel";
            this.downloadBarLabel.Size = new System.Drawing.Size(114, 15);
            this.downloadBarLabel.TabIndex = 5;
            this.downloadBarLabel.Text = "Items: Not Updating";
            // 
            // downloadButton
            // 
            this.downloadButton.Location = new System.Drawing.Point(125, 317);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(144, 23);
            this.downloadButton.TabIndex = 6;
            this.downloadButton.Text = "Start Downloading";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            // 
            // Downloader
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 347);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.downloadBarLabel);
            this.Controls.Add(this.downloadBar);
            this.Controls.Add(this.downloadBoxLog);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Name = "Downloader";
            this.Text = "Downloader";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TextBox textBox1;
        private TextBox textBox2;
        private Label label1;
        private TextBox downloadBoxLog;
        private ProgressBar downloadBar;
        private Label downloadBarLabel;
        private Button downloadButton;
    }
}