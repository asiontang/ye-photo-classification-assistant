﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace 照片分类辅助器
{
    public partial class MainForm : Form
    {
        string[] fileList = null;

        private string SpeculateNewFolderName(string path)
        {
            //现在统一的文件命名是yyyy-MM-dd HH:mm:ss
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName == null || fileName.Length < 11)
                return null;
            DateTime dt;
            if (!DateTime.TryParse(fileName.Remove(10), out dt))
                return null;
            return dt.ToString("yyMMdd") + " - ";
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Move;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            fileList = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (fileList == null || fileList.Length == 0)
                return;
            var newFolderName = SpeculateNewFolderName(fileList[0]);
            if (string.IsNullOrEmpty(newFolderName))
            {
                this.TopMost = false;
                MessageBox.Show("文件名格式不正确！");
                this.TopMost = true;
                return;
            }

            label1.Text = string.Format("待移动文件个数：{0}", fileList.Length);

            cbbNewFolderName.Text = newFolderName;
        }

        private void label1_TextChanged(object sender, EventArgs e)
        {
            label1.Visible = label1.Text != "";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            //当下拉框里面的是文件夹路径时.执行自动分类功能。
            if (Directory.Exists(cbbNewFolderName.Text))
            {
                AutoClassify(cbbNewFolderName.Text);
            }
            else
            {
                //添加下拉列表
                if (!cbbNewFolderName.Items.Contains(cbbNewFolderName.Text))
                    cbbNewFolderName.Items.Add(cbbNewFolderName.Text);
                MoveFiles(cbbNewFolderName.Text, fileList);
            }
            Cursor.Current = Cursors.Default;
        }

        private void MoveFiles(string dirName, string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            var newFolderPath = Path.GetDirectoryName(files[0]);
            newFolderPath = Path.Combine(newFolderPath, dirName);
            Directory.CreateDirectory(newFolderPath);

            foreach (string file in files)
            {
                var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(file));
                File.Move(file, newFilePath);
            }
        }

        private void AutoClassify(String path)
        {
            //首先读取该目录下的所有文件
            var files = new List<string>(Directory.GetFiles(path));
            files.Sort();
            var dirs = new Dictionary<string, List<string>>();

            progressBar1.Maximum = files.Count;
            progressBar1.Value = 0;
            foreach (var file in files)
            {
                var folderName = SpeculateNewFolderName(file);
                if (string.IsNullOrEmpty(folderName))
                    continue;

                if (!dirs.ContainsKey(folderName))
                    dirs.Add(folderName, new List<string>());

                dirs[folderName].Add(file);

                progressBar1.PerformStep();
            }
            if (dirs.Count == 0)
            {
                MessageBox.Show("没有可自动分类的文件！");
                return;
            }

            if (MessageBox.Show(string.Format("检测到{0}个可归类文件夹，是否马上将文件归类？", dirs.Count)
                   , "", MessageBoxButtons.YesNo) == DialogResult.No)
                return;

            progressBar1.Maximum = dirs.Count;
            progressBar1.Value = 0;
            foreach (var dir in dirs)
            {
                progressBar1.PerformStep();
                if (dir.Value.Count < numericUpDown1.Value)
                    continue;
                MoveFiles(dir.Key, dir.Value.ToArray());
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnOK_Click(btnOK, EventArgs.Empty);
        }
    }
}
