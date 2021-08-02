using System;
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
            //假如拖进来的是文件夹
            if (Directory.Exists(fileList[0]))
            {
                cbbNewFolderName.Text = fileList[0];
                return;
            }

            //当拖进来的是一些零散的文件时.
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
            try
            {
                //自动去掉路径里的左右""引号
                cbbNewFolderName.Text = cbbNewFolderName.Text.Trim().Trim(new[] { '"', ' ' });

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            Cursor.Current = Cursors.Default;
        }

        private void MoveFiles(string dirName, string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            String newFolderPath;
            //当下拉框里面的是文件夹路径时.自动分类的文件夹都放在这个路径下.
            if (Directory.Exists(cbbNewFolderName.Text))
                newFolderPath = Path.Combine(cbbNewFolderName.Text, dirName);
            else
                newFolderPath = Path.Combine(Path.GetDirectoryName(files[0]), dirName);
            Directory.CreateDirectory(newFolderPath);

            foreach (string file in files)
            {
                var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(file));

                //当文件存在时,自动在文件名后面加1
                int StepCount = 1;
                while (File.Exists(newFilePath))
                {
                    newFilePath = Path.Combine(newFolderPath, Path.GetFileNameWithoutExtension(file) + "_" + (++StepCount) + Path.GetExtension(file));
                }
                File.Move(file, newFilePath);
            }
        }

        private void AutoClassify(String path)
        {
            //首先读取该目录下的所有文件
            var files = new List<string>(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
            files.Sort();
            var dirs = new Dictionary<string, List<string>>();

            progressBar1.Maximum = files.Count;
            progressBar1.Value = 0;
            foreach (var file in files)
            {
                var folderName = SpeculateNewFolderName(file);
                if (string.IsNullOrEmpty(folderName))
                {
                    //当文件存在时,自动在文件名后面加1
                    int StepCount = 1;

                    //将不符合规则的文件,移动到根目录.方便归纳整理.
                    String newFilePath = Path.Combine(path, Path.GetFileName(file));
                    while (File.Exists(newFilePath)) 
                    {
                        newFilePath = Path.Combine(path, Path.GetFileNameWithoutExtension(file) + "_" + (++StepCount) + Path.GetExtension(file));
                    }
                    File.Move(file, newFilePath);
                    continue;
                }

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

            //清理空文件夹
            var folders = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            var folderList = new List<string>(folders);
            folderList.Sort(delegate (string x, string y)
            {
                int xSize = x.Length;
                int ySize = y.Length;
                if (xSize == ySize)
                    return 0;
                if (xSize > ySize)
                    //将字符串长度长的文件夹排在最前面,这样就会优先将子文件夹先删除,再删除父文件夹.
                    //否则可能会出现删除父文件夹时,报错提示文件夹不为空的异常.
                    return -1;
                else
                    return 1;
            });
            var allDir = "";
            int Count = 0;

            foreach (var dir in folderList)
            {
                if (!Directory.Exists(dir))
                    continue;
                if (Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).Length > 0)
                    continue;
                if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length > 0)
                    continue;
                Directory.Delete(dir, false);
                allDir += ++Count + ". " + dir + "\r\n";
            }
            MessageBox.Show("已删除以下" + Count + "个空文件夹:\r\n\r\n" + allDir);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnOK_Click(btnOK, EventArgs.Empty);
        }
    }
}
