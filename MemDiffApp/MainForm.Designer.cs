namespace MemDiffApp
{
    partial class MainForm
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
            button_analyze = new Button();
            button2_selfile = new Button();
            button1_selfile = new Button();
            memreport2 = new Label();
            textBox_file2 = new TextBox();
            memreport1 = new Label();
            textBox_file1 = new TextBox();
            SuspendLayout();
            // 
            // button_analyze
            // 
            button_analyze.Location = new Point(13, 87);
            button_analyze.Name = "button_analyze";
            button_analyze.Size = new Size(94, 29);
            button_analyze.TabIndex = 13;
            button_analyze.Text = "Analyze";
            button_analyze.UseVisualStyleBackColor = true;
            button_analyze.Click += button_analyze_Click;
            // 
            // button2_selfile
            // 
            button2_selfile.Location = new Point(668, 44);
            button2_selfile.Name = "button2_selfile";
            button2_selfile.Size = new Size(94, 29);
            button2_selfile.TabIndex = 12;
            button2_selfile.Text = "open";
            button2_selfile.UseVisualStyleBackColor = true;
            button2_selfile.Click += button2_selfile_Click;
            // 
            // button1_selfile
            // 
            button1_selfile.Location = new Point(668, 10);
            button1_selfile.Name = "button1_selfile";
            button1_selfile.Size = new Size(94, 29);
            button1_selfile.TabIndex = 11;
            button1_selfile.Text = "open";
            button1_selfile.UseVisualStyleBackColor = true;
            button1_selfile.Click += button1_selfile_Click;
            // 
            // memreport2
            // 
            memreport2.AutoSize = true;
            memreport2.Location = new Point(13, 48);
            memreport2.Name = "memreport2";
            memreport2.Size = new Size(106, 20);
            memreport2.TabIndex = 10;
            memreport2.Text = "memreport2:";
            // 
            // textBox_file2
            // 
            textBox_file2.Location = new Point(130, 45);
            textBox_file2.Name = "textBox_file2";
            textBox_file2.Size = new Size(532, 27);
            textBox_file2.TabIndex = 9;
            // 
            // memreport1
            // 
            memreport1.AutoSize = true;
            memreport1.Location = new Point(13, 15);
            memreport1.Name = "memreport1";
            memreport1.Size = new Size(106, 20);
            memreport1.TabIndex = 8;
            memreport1.Text = "memreport1:";
            // 
            // textBox_file1
            // 
            textBox_file1.Location = new Point(130, 12);
            textBox_file1.Name = "textBox_file1";
            textBox_file1.Size = new Size(532, 27);
            textBox_file1.TabIndex = 7;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(button_analyze);
            Controls.Add(button2_selfile);
            Controls.Add(button1_selfile);
            Controls.Add(memreport2);
            Controls.Add(textBox_file2);
            Controls.Add(memreport1);
            Controls.Add(textBox_file1);
            Name = "MainForm";
            Text = "MainForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button_analyze;
        private Button button2_selfile;
        private Button button1_selfile;
        private Label memreport2;
        private TextBox textBox_file2;
        private Label memreport1;
        public TextBox textBox_file1;
    }
}