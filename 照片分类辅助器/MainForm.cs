using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
            {
                //转换时间戳格式的文件名为正常的年月日时间
                var newFilePath = convertTimestamp(path);
                if (path == newFilePath)
                    return null;
                else
                    return SpeculateNewFolderName(newFilePath);
            }
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

            //去掉后面的空格:[210801 - ]实际创建的是[210801 -].假如不去掉,比对的新旧文件路径始终会不一致,就会出现多个_2_2了.
            newFolderPath = newFolderPath.Trim();

            Directory.CreateDirectory(newFolderPath);

            foreach (string file in files)
            {
                var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(file));

                //当原地移动时,就忽略掉.否则会出现多次移动文件名多了一堆_2_2_2
                if (file == newFilePath)
                    continue;

                newFilePath = convertTimestamp(newFilePath);

                //当文件存在时,自动在文件名后面加1
                int StepCount = 1;
                while (File.Exists(newFilePath))
                {
                    newFilePath = Path.Combine(newFolderPath, Path.GetFileNameWithoutExtension(file) + "_" + (++StepCount) + Path.GetExtension(file));
                }
                File.Move(file, newFilePath);
            }
        }
        /// <summary>
        /// 检测文件名里是否有时间戳,返回正常年月日格式的新文件名
        /// </summary>
        /// <param name="oldFile"></param>
        /// <returns>没有时间戳时,返回原文件名</returns>
        private string convertTimestamp(string oldFile)
        {
            //检测文件名中是否携带这UNIX时间戳
            var oldFileName = Path.GetFileNameWithoutExtension(oldFile);
            String timestamp = null;
            {
                //检测是否是13位数的带了毫秒位数的时间戳(无则增加毫秒位)
                var match = Regex.Match(oldFileName, "(\\d{13})");
                if (match.Success)
                    timestamp = match.Value;
                else
                {
                    match = Regex.Match(oldFileName, "(\\d{10})");
                    if (match.Success)
                        timestamp = match.Value + "000";
                }
                if (String.IsNullOrEmpty(timestamp))
                    return oldFile;
            }

            //将时间戳转换为指定时间格式字符串
            var start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var dateTimeResult = start.AddMilliseconds(long.Parse(timestamp)).ToLocalTime();

            //不能大于当前时间,一般整理的文件都是历史的,过去的.不会是未来的.
            if (dateTimeResult.CompareTo(DateTime.Now) > 0)
                return oldFile;

            //不能小于2010年前的日志,历史太久远,理应不会整理那么老的文件.
            if (dateTimeResult.CompareTo(new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Local)) < 0)
                return oldFile;

            //当原文件名已经包含指定的前缀,为了防止重复.
            if (oldFileName.StartsWith(dateTimeResult.ToString("yyyy-MM-dd")))
                return oldFile;

            //转换为新文件名
            var newFileName = string.Format("{0} {1}{2}", dateTimeResult.ToString("yyyy-MM-dd HHmmss"), oldFileName, Path.GetExtension(oldFile));
            var newFile = Path.Combine(Path.GetDirectoryName(oldFile), newFileName);
            return newFile;
        }

        private void AutoClassify(String path)
        {
            //首先读取该目录下的所有文件
            var files = new List<string>(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
            files.Sort();
            var dirs = new Dictionary<string, List<string>>();

            progressBar1.Maximum = files.Count;
            progressBar1.Value = 0;
            int unClassifyCount = 0;
            foreach (var file in files)
            {
                var folderName = SpeculateNewFolderName(file);
                if (string.IsNullOrEmpty(folderName))
                {
                    unClassifyCount++;
                    //将不符合规则的文件,移动到根目录.方便归纳整理.
                    String newFilePath = Path.Combine(path, Path.GetFileName(file));

                    //当原地移动时,就忽略掉.否则会出现多次移动文件名多了一堆_2_2_2
                    if (file == newFilePath)
                        continue;

                    //当文件存在时,自动在文件名后面加1
                    int StepCount = 1;
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
            int deletedEmptyFolderCount = 0;

            foreach (var dir in folderList)
            {
                if (!Directory.Exists(dir))
                    continue;
                if (Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).Length > 0)
                    continue;
                if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length > 0)
                    continue;
                Directory.Delete(dir, false);
                allDir += ++deletedEmptyFolderCount + ". " + dir + "\r\n";
            }
            MessageBox.Show("已删除以下" + deletedEmptyFolderCount + "个空文件夹:\r\n\r\n" + allDir);

            label1.Text = string.Format(""
                + "检测到文件:{0}个              可分类目录:{1}个"
                + "\n\n"
                + "未分类文件:{2}个              已删除目录:{3}个"
                , files.Count
                , dirs.Count
                , unClassifyCount
                , deletedEmptyFolderCount);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnOK_Click(btnOK, EventArgs.Empty);
        }
    }
}
