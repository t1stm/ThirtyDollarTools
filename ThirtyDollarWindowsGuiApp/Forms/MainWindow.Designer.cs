namespace ThirtyDollarWindowsGuiApp.Forms
{
    partial class MainWindow
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel4 = new System.Windows.Forms.Panel();
            this.saveLocationButton = new System.Windows.Forms.Button();
            this.sequenceLocationButton = new System.Windows.Forms.Button();
            this.saveLocationBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.sequenceLocationBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.logCheckbox = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.logBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.panel3 = new System.Windows.Forms.Panel();
            this.previewSequenceButton = new System.Windows.Forms.Button();
            this.startConvertingButton = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.panel4);
            this.panel1.Controls.Add(this.saveLocationButton);
            this.panel1.Controls.Add(this.sequenceLocationButton);
            this.panel1.Controls.Add(this.saveLocationBox);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.sequenceLocationBox);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Location = new System.Drawing.Point(12, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(335, 90);
            this.panel1.TabIndex = 0;
            // 
            // panel4
            // 
            this.panel4.Location = new System.Drawing.Point(0, 91);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(200, 100);
            this.panel4.TabIndex = 2;
            // 
            // saveLocationButton
            // 
            this.saveLocationButton.Location = new System.Drawing.Point(253, 62);
            this.saveLocationButton.Name = "saveLocationButton";
            this.saveLocationButton.Size = new System.Drawing.Size(75, 23);
            this.saveLocationButton.TabIndex = 5;
            this.saveLocationButton.Text = "Save";
            this.saveLocationButton.UseVisualStyleBackColor = true;
            this.saveLocationButton.Click += new System.EventHandler(this.saveLocationButton_Click);
            // 
            // sequenceLocationButton
            // 
            this.sequenceLocationButton.Location = new System.Drawing.Point(253, 18);
            this.sequenceLocationButton.Name = "sequenceLocationButton";
            this.sequenceLocationButton.Size = new System.Drawing.Size(75, 23);
            this.sequenceLocationButton.TabIndex = 4;
            this.sequenceLocationButton.Text = "Sequence";
            this.sequenceLocationButton.UseVisualStyleBackColor = true;
            this.sequenceLocationButton.Click += new System.EventHandler(this.sequenceLocationButton_Click);
            // 
            // saveLocationBox
            // 
            this.saveLocationBox.Location = new System.Drawing.Point(3, 62);
            this.saveLocationBox.Name = "saveLocationBox";
            this.saveLocationBox.Size = new System.Drawing.Size(244, 23);
            this.saveLocationBox.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 44);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(126, 15);
            this.label4.TabIndex = 2;
            this.label4.Text = "Audio File Destination:";
            // 
            // sequenceLocationBox
            // 
            this.sequenceLocationBox.Location = new System.Drawing.Point(3, 18);
            this.sequenceLocationBox.Name = "sequenceLocationBox";
            this.sequenceLocationBox.Size = new System.Drawing.Size(244, 23);
            this.sequenceLocationBox.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(110, 15);
            this.label3.TabIndex = 0;
            this.label3.Text = "Sequence Location:";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.logCheckbox);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Controls.Add(this.logBox);
            this.panel2.Controls.Add(this.label1);
            this.panel2.Controls.Add(this.progressBar);
            this.panel2.Location = new System.Drawing.Point(12, 108);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(335, 218);
            this.panel2.TabIndex = 1;
            // 
            // logCheckbox
            // 
            this.logCheckbox.AutoSize = true;
            this.logCheckbox.Location = new System.Drawing.Point(3, 154);
            this.logCheckbox.Name = "logCheckbox";
            this.logCheckbox.Size = new System.Drawing.Size(286, 19);
            this.logCheckbox.TabIndex = 4;
            this.logCheckbox.Text = "Log Every Event (Slows Down The Program A Lot)";
            this.logCheckbox.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Log:";
            // 
            // logBox
            // 
            this.logBox.BackColor = System.Drawing.SystemColors.Control;
            this.logBox.Font = new System.Drawing.Font("Cascadia Code", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.logBox.Location = new System.Drawing.Point(3, 18);
            this.logBox.MaxLength = 2147483647;
            this.logBox.Multiline = true;
            this.logBox.Name = "logBox";
            this.logBox.ReadOnly = true;
            this.logBox.Size = new System.Drawing.Size(329, 130);
            this.logBox.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 174);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Progress:";
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(3, 192);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(329, 23);
            this.progressBar.TabIndex = 2;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.previewSequenceButton);
            this.panel3.Controls.Add(this.startConvertingButton);
            this.panel3.Location = new System.Drawing.Point(12, 332);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(335, 39);
            this.panel3.TabIndex = 0;
            // 
            // previewSequenceButton
            // 
            this.previewSequenceButton.Location = new System.Drawing.Point(172, 3);
            this.previewSequenceButton.Name = "previewSequenceButton";
            this.previewSequenceButton.Size = new System.Drawing.Size(160, 30);
            this.previewSequenceButton.TabIndex = 5;
            this.previewSequenceButton.Text = "Preview Sequence";
            this.previewSequenceButton.UseVisualStyleBackColor = true;
            this.previewSequenceButton.Click += new System.EventHandler(this.previewSequenceButton_Click);
            // 
            // startConvertingButton
            // 
            this.startConvertingButton.Location = new System.Drawing.Point(3, 3);
            this.startConvertingButton.Name = "startConvertingButton";
            this.startConvertingButton.Size = new System.Drawing.Size(160, 30);
            this.startConvertingButton.TabIndex = 4;
            this.startConvertingButton.Text = "Start Converting";
            this.startConvertingButton.UseVisualStyleBackColor = true;
            this.startConvertingButton.Click += new System.EventHandler(this.startConvertingButton_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(359, 376);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximumSize = new System.Drawing.Size(375, 415);
            this.MinimumSize = new System.Drawing.Size(375, 415);
            this.Name = "MainWindow";
            this.Text = "Thirty Dollar Converter";
            this.Load += new System.EventHandler(this.MainWindow_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Panel panel1;
        private Panel panel2;
        private Label label2;
        private TextBox logBox;
        private Label label1;
        private ProgressBar progressBar;
        private Panel panel3;
        private Panel panel4;
        private Button saveLocationButton;
        private Button sequenceLocationButton;
        private TextBox saveLocationBox;
        private Label label4;
        private TextBox sequenceLocationBox;
        private Label label3;
        private Button previewSequenceButton;
        private Button startConvertingButton;
        private CheckBox logCheckbox;
    }
}