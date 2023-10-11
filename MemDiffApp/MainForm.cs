using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MemDiffApp
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public string GetSelectedFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;//该值确定是否可以选择多个文件
            dialog.Title = "请选择文件";
            dialog.Filter = "所有文件(*.memreport)|*.memreport";
            dialog.InitialDirectory = @"E:\";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.FileName;
            }
            return null;
        }

        private void button1_selfile_Click(object sender, EventArgs e)
        {
            this.textBox_file1.Text = GetSelectedFile();
        }

        private void button2_selfile_Click(object sender, EventArgs e)
        {
            this.textBox_file2.Text = GetSelectedFile();
        }

        private void button_analyze_Click(object sender, EventArgs e)
        {

        }
    }
}
