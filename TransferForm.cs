using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace OPhoneMirror
{
    internal sealed class FileEntry
    {
        public string Name;
        public string FullPath;
        public bool IsDirectory;
        public DateTime Modified;
        public long Size;
    }

    internal sealed class TransferJob
    {
        public bool ToPhone;
        public FileEntry Entry;
        public string Source;
        public string DestinationDirectory;
        public string DestinationDisplay;
        public bool Overwrite;
        public int RowIndex;
        public AdbResult Result;
    }

    internal sealed class TransferForm : Form
    {
        private readonly Color page = UiTheme.Background;
        private readonly Color panelColor = UiTheme.Surface;
        private readonly Color border = UiTheme.Border;
        private readonly Color text = UiTheme.Text;
        private readonly Color muted = UiTheme.TextMuted;
        private readonly Color accent = UiTheme.Accent;
        private readonly Color success = UiTheme.Success;

        private readonly string deviceName;
        private readonly AdbClient adb;
        private readonly Panel localPanel;
        private readonly Panel remotePanel;
        private readonly RoundedTextBox localPathBox;
        private readonly RoundedTextBox remotePathBox;
        private readonly DataGridView localGrid;
        private readonly DataGridView remoteGrid;
        private readonly DataGridView taskGrid;
        private readonly Button sendButton;
        private readonly Button receiveButton;
        private readonly Button clearTasksButton;
        private readonly ProgressBar progress;
        private readonly Label statusLabel;
        private readonly BackgroundWorker transferWorker;
        private readonly ToolTip toolTip;
        private Button remoteMoreButton;

        private string localPath;
        private string remotePath = "/sdcard/Download";
        private int remoteLoadGeneration;
        private List<FileEntry> remoteEntries = new List<FileEntry>();
        private int remoteDisplayedCount;
        private const int RemoteBatchSize = 1000;

        public TransferForm(string deviceName, string serial, string adbPath)
        {
            this.deviceName = deviceName;
            adb = new AdbClient(adbPath, serial);
            toolTip = new ToolTip();

            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            localPath = Directory.Exists(downloads)
                ? downloads
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            Text = "文件互传 · " + deviceName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(1140, 640);
            BackColor = page;
            ForeColor = text;
            Font = new Font(UiTheme.FontFamily, 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Label title = new Label();
            title.Text = "文件互传";
            title.Font = new Font(UiTheme.FontFamily, 21F, FontStyle.Bold);
            title.ForeColor = text;
            title.AutoSize = true;
            title.Location = new Point(20, 18);
            Controls.Add(title);

            int badgeWidth = Math.Max(112, TextRenderer.MeasureText(deviceName, Font).Width + 50);
            RoundedPanel deviceBadge = new RoundedPanel();
            deviceBadge.BackColor = Color.FromArgb(234, 247, 237);
            deviceBadge.BorderColor = Color.FromArgb(198, 233, 207);
            deviceBadge.CornerRadius = 15;
            deviceBadge.Location = new Point(190, 21);
            deviceBadge.Size = new Size(badgeWidth, 30);
            Controls.Add(deviceBadge);

            StatusDot deviceDot = new StatusDot();
            deviceDot.DotColor = success;
            deviceDot.Location = new Point(12, 10);
            deviceDot.Size = new Size(10, 10);
            deviceBadge.Controls.Add(deviceDot);

            Label deviceLabel = new Label();
            deviceLabel.Text = "USB · " + deviceName;
            deviceLabel.ForeColor = success;
            deviceLabel.AutoSize = true;
            deviceLabel.Location = new Point(29, 6);
            deviceBadge.Controls.Add(deviceLabel);

            sendButton = MakeTransferButton("发送到手机");
            sendButton.Click += delegate { StartTransfer(true); };
            Controls.Add(sendButton);

            receiveButton = MakeTransferButton("保存到电脑");
            receiveButton.Click += delegate { StartTransfer(false); };
            Controls.Add(receiveButton);

            localPanel = MakePane();
            remotePanel = MakePane();
            Controls.Add(localPanel);
            Controls.Add(remotePanel);

            localPathBox = MakePathBox();
            localPathBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    NavigateLocal(localPathBox.Text);
                    e.SuppressKeyPress = true;
                }
            };
            remotePathBox = MakePathBox();
            remotePathBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    NavigateRemote(remotePathBox.Text);
                    e.SuppressKeyPress = true;
                }
            };

            localGrid = MakeFileGrid();
            remoteGrid = MakeFileGrid();
            localGrid.CellDoubleClick += LocalGridDoubleClick;
            remoteGrid.CellDoubleClick += RemoteGridDoubleClick;

            BuildLocalPane();
            BuildRemotePane();

            RoundedPanel taskPanel = new RoundedPanel();
            taskPanel.Name = "taskPanel";
            taskPanel.BackColor = panelColor;
            taskPanel.BorderColor = Color.FromArgb(232, 232, 236);
            taskPanel.Shadow = true;
            Controls.Add(taskPanel);

            Label taskTitle = new Label();
            taskTitle.Text = "传输列表";
            taskTitle.Font = new Font(UiTheme.FontFamily, 12F, FontStyle.Bold);
            taskTitle.AutoSize = true;
            taskTitle.Location = new Point(14, 12);
            taskPanel.Controls.Add(taskTitle);

            clearTasksButton = MakeToolbarIcon(UiIcon.Trash, "清除记录");
            clearTasksButton.Click += delegate { taskGrid.Rows.Clear(); };
            taskPanel.Controls.Add(clearTasksButton);

            taskGrid = MakeTaskGrid();
            taskPanel.Controls.Add(taskGrid);

            progress = new ProgressBar();
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            progress.Visible = false;
            taskPanel.Controls.Add(progress);

            statusLabel = new Label();
            statusLabel.Text = "选择一侧的文件或文件夹，再发送到另一侧当前目录";
            statusLabel.ForeColor = muted;
            statusLabel.AutoEllipsis = true;
            taskPanel.Controls.Add(statusLabel);
            statusLabel.BringToFront();

            transferWorker = new BackgroundWorker();
            transferWorker.DoWork += TransferWorkerDoWork;
            transferWorker.RunWorkerCompleted += TransferWorkerCompleted;

            Resize += delegate { LayoutControls(); };
            FormClosed += delegate { toolTip.Dispose(); };
            Shown += delegate
            {
                LayoutControls();
                RefreshLocal();
                RefreshRemote();
            };
            LayoutControls();
        }

        private void BuildLocalPane()
        {
            Label heading = MakePaneHeading("这台电脑");
            localPanel.Controls.Add(heading);
            localPanel.Controls.Add(MakePaneCaption("选择源文件，或选择文件的接收目录"));
            localPanel.Controls.Add(localPathBox);

            Button up = MakeToolbarIcon(UiIcon.ArrowUp, "上一级");
            up.Location = new Point(14, 110);
            up.Click += delegate
            {
                DirectoryInfo parent = Directory.GetParent(localPath);
                if (parent != null)
                    NavigateLocal(parent.FullName);
            };
            localPanel.Controls.Add(up);

            Button refresh = MakeToolbarIcon(UiIcon.Refresh, "刷新");
            refresh.Location = new Point(56, 110);
            refresh.Click += delegate { RefreshLocal(); };
            localPanel.Controls.Add(refresh);

            Button browse = MakeToolbarIcon(UiIcon.Folder, "选择电脑目录");
            browse.Location = new Point(98, 110);
            browse.Click += BrowseLocal;
            localPanel.Controls.Add(browse);

            Button newFolder = MakeToolbarButton("新建文件夹");
            newFolder.Location = new Point(140, 110);
            newFolder.Size = new Size(104, 30);
            newFolder.Click += CreateLocalFolder;
            localPanel.Controls.Add(newFolder);

            localGrid.Location = new Point(14, 150);
            localPanel.Controls.Add(localGrid);
        }

        private void BuildRemotePane()
        {
            Label heading = MakePaneHeading(deviceName);
            remotePanel.Controls.Add(heading);
            remotePanel.Controls.Add(MakePaneCaption("手机共享存储，可直接前往下载或相册目录"));
            remotePanel.Controls.Add(remotePathBox);

            Button up = MakeToolbarIcon(UiIcon.ArrowUp, "上一级");
            up.Location = new Point(14, 110);
            up.Click += delegate { NavigateRemote(AdbClient.RemoteParent(remotePath)); };
            remotePanel.Controls.Add(up);

            Button refresh = MakeToolbarIcon(UiIcon.Refresh, "刷新");
            refresh.Location = new Point(56, 110);
            refresh.Click += delegate { RefreshRemote(); };
            remotePanel.Controls.Add(refresh);

            Button downloads = MakeToolbarButton("Download");
            downloads.Location = new Point(98, 110);
            downloads.Size = new Size(92, 30);
            downloads.Click += delegate { NavigateRemote("/sdcard/Download"); };
            remotePanel.Controls.Add(downloads);

            Button newFolder = MakeToolbarButton("新建文件夹");
            newFolder.Location = new Point(198, 110);
            newFolder.Size = new Size(96, 30);
            newFolder.Click += CreateRemoteFolder;
            remotePanel.Controls.Add(newFolder);

            Button dcim = MakeToolbarButton("DCIM");
            dcim.Location = new Point(302, 110);
            dcim.Size = new Size(62, 30);
            dcim.Click += delegate { NavigateRemote("/sdcard/DCIM"); };
            remotePanel.Controls.Add(dcim);

            remoteMoreButton = MakeToolbarIcon(UiIcon.More, "显示更多文件");
            remoteMoreButton.Location = new Point(372, 110);
            remoteMoreButton.Enabled = false;
            remoteMoreButton.Click += delegate { AddRemoteBatch(); };
            remotePanel.Controls.Add(remoteMoreButton);

            remoteGrid.Location = new Point(14, 150);
            remotePanel.Controls.Add(remoteGrid);
        }

        private void LayoutControls()
        {
            int margin = 20;
            int gap = 18;
            int paneTop = 76;
            int paneHeight = Math.Max(310, (ClientSize.Height - 150) * 57 / 100);
            int paneWidth = (ClientSize.Width - margin * 2 - gap) / 2;

            localPanel.SetBounds(margin, paneTop, paneWidth, paneHeight);
            remotePanel.SetBounds(margin + paneWidth + gap, paneTop, paneWidth, paneHeight);

            localPathBox.SetBounds(14, 70, paneWidth - 28, 30);
            remotePathBox.SetBounds(14, 70, paneWidth - 28, 30);
            localGrid.Size = new Size(paneWidth - 28, paneHeight - 164);
            remoteGrid.Size = new Size(paneWidth - 28, paneHeight - 164);

            int center = ClientSize.Width / 2;
            sendButton.SetBounds(center - 238, 18, 170, 38);
            receiveButton.SetBounds(center + 68, 18, 170, 38);

            Panel taskPanel = Controls["taskPanel"] as Panel;
            if (taskPanel != null)
            {
                int taskTop = paneTop + paneHeight + 16;
                int taskHeight = ClientSize.Height - taskTop - margin;
                taskPanel.SetBounds(margin, taskTop, ClientSize.Width - margin * 2, taskHeight);
                clearTasksButton.SetBounds(taskPanel.Width - 50, 9, 34, 31);
                statusLabel.SetBounds(126, 17, taskPanel.Width - 196, 22);
                progress.SetBounds(14, 45, taskPanel.Width - 28, 4);
                taskGrid.SetBounds(14, 55, taskPanel.Width - 28, Math.Max(80, taskPanel.Height - 69));
            }
        }

        private Panel MakePane()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.BackColor = panelColor;
            panel.BorderColor = Color.FromArgb(232, 232, 236);
            panel.Shadow = true;
            return panel;
        }

        private Label MakePaneHeading(string heading)
        {
            Label label = new Label();
            label.Text = heading;
            label.Font = new Font(UiTheme.FontFamily, 12F, FontStyle.Bold);
            label.ForeColor = text;
            label.AutoSize = true;
            label.Location = new Point(14, 12);
            return label;
        }

        private Label MakePaneCaption(string caption)
        {
            Label label = new Label();
            label.Text = caption;
            label.Font = new Font(UiTheme.FontFamily, 8.5F);
            label.ForeColor = muted;
            label.AutoSize = true;
            label.Location = new Point(15, 42);
            return label;
        }

        private RoundedTextBox MakePathBox()
        {
            RoundedTextBox box = new RoundedTextBox();
            return box;
        }

        private Button MakeTransferButton(string caption)
        {
            ModernButton button = new ModernButton();
            button.Text = caption;
            button.Kind = UiButtonKind.Primary;
            button.BackColor = accent;
            button.ForeColor = Color.White;
            button.Font = new Font(UiTheme.FontFamily, 9.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Button MakeToolbarButton(string caption)
        {
            ModernButton button = new ModernButton();
            button.Text = caption;
            button.Kind = UiButtonKind.Secondary;
            button.Size = new Size(78, 30);
            button.BackColor = panelColor;
            button.ForeColor = text;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Button MakeToolbarIcon(UiIcon icon, string accessibleName)
        {
            ModernButton button = (ModernButton)MakeToolbarButton(string.Empty);
            button.Icon = icon;
            button.IconOnly = true;
            button.IconSize = 18;
            button.AccessibleName = accessibleName;
            button.Size = new Size(34, 30);
            button.CornerRadius = 9;
            toolTip.SetToolTip(button, accessibleName);
            return button;
        }

        private DataGridView MakeFileGrid()
        {
            DataGridView grid = new DataGridView();
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = UiTheme.Border;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeight = 38;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SurfaceRaised;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.SurfaceRaised;
            grid.DefaultCellStyle.BackColor = UiTheme.Surface;
            grid.DefaultCellStyle.ForeColor = text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 237, 255);
            grid.DefaultCellStyle.SelectionForeColor = text;
            grid.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 36;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.AutoGenerateColumns = false;

            DataGridViewCheckBoxColumn check = new DataGridViewCheckBoxColumn();
            check.Width = 38;
            check.HeaderText = "";
            grid.Columns.Add(check);

            DataGridViewTextBoxColumn name = new DataGridViewTextBoxColumn();
            name.HeaderText = "名称";
            name.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            name.FillWeight = 50;
            grid.Columns.Add(name);

            DataGridViewTextBoxColumn modified = new DataGridViewTextBoxColumn();
            modified.HeaderText = "修改日期";
            modified.Width = 142;
            grid.Columns.Add(modified);

            DataGridViewTextBoxColumn type = new DataGridViewTextBoxColumn();
            type.HeaderText = "类型";
            type.Width = 74;
            grid.Columns.Add(type);

            DataGridViewTextBoxColumn size = new DataGridViewTextBoxColumn();
            size.HeaderText = "大小";
            size.Width = 88;
            size.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(size);

            return grid;
        }

        private DataGridView MakeTaskGrid()
        {
            DataGridView grid = new DataGridView();
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = UiTheme.Border;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeight = 34;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SurfaceRaised;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = UiTheme.Surface;
            grid.DefaultCellStyle.ForeColor = text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 237, 255);
            grid.DefaultCellStyle.SelectionForeColor = text;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 30;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Add("name", "名称");
            grid.Columns.Add("direction", "方向");
            grid.Columns.Add("status", "状态");
            grid.Columns.Add("size", "大小");
            grid.Columns.Add("source", "发送路径");
            grid.Columns.Add("destination", "接收路径");
            grid.Columns.Add("result", "结果");
            grid.Columns[0].FillWeight = 16;
            grid.Columns[1].FillWeight = 10;
            grid.Columns[2].FillWeight = 10;
            grid.Columns[3].FillWeight = 9;
            grid.Columns[4].FillWeight = 25;
            grid.Columns[5].FillWeight = 25;
            grid.Columns[6].FillWeight = 18;
            return grid;
        }

        private void NavigateLocal(string path)
        {
            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
                if (!Directory.Exists(full))
                    throw new DirectoryNotFoundException("电脑目录不存在：" + full);
                localPath = full;
                RefreshLocal();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "无法打开电脑目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                localPathBox.Text = localPath;
            }
        }

        private void NavigateRemote(string path)
        {
            remotePath = AdbClient.NormalizeRemotePath(path);
            RefreshRemote();
        }

        private void RefreshLocal()
        {
            localPathBox.Text = localPath;
            localGrid.Rows.Clear();

            try
            {
                DirectoryInfo directory = new DirectoryInfo(localPath);
                List<FileEntry> entries = new List<FileEntry>();
                foreach (DirectoryInfo item in directory.GetDirectories())
                {
                    entries.Add(new FileEntry
                    {
                        Name = item.Name,
                        FullPath = item.FullName,
                        IsDirectory = true,
                        Modified = item.LastWriteTime,
                        Size = 0
                    });
                }
                foreach (FileInfo item in directory.GetFiles())
                {
                    entries.Add(new FileEntry
                    {
                        Name = item.Name,
                        FullPath = item.FullName,
                        IsDirectory = false,
                        Modified = item.LastWriteTime,
                        Size = item.Length
                    });
                }
                PopulateGrid(localGrid, entries);
                statusLabel.Text = "电脑：" + entries.Count + " 项";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "电脑目录读取失败：" + ex.Message;
            }
        }

        private void RefreshRemote()
        {
            remotePath = AdbClient.NormalizeRemotePath(remotePath);
            remotePathBox.Text = remotePath;
            remoteGrid.Enabled = false;
            remoteGrid.Rows.Clear();
            remoteEntries.Clear();
            remoteDisplayedCount = 0;
            remoteMoreButton.Enabled = false;
            statusLabel.Text = "正在读取手机目录…";
            int generation = ++remoteLoadGeneration;

            ThreadPool.QueueUserWorkItem(delegate
            {
                AdbResult result = adb.ListRemote(remotePath);
                List<FileEntry> entries = result.Success
                    ? ParseRemoteEntries(result.Output)
                    : new List<FileEntry>();

                if (IsDisposed)
                    return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (generation != remoteLoadGeneration)
                        return;
                    remoteGrid.Enabled = true;
                    if (!result.Success)
                    {
                        statusLabel.Text = "手机目录读取失败：" + result.Message;
                        return;
                    }
                    remoteEntries = entries;
                    remoteDisplayedCount = 0;
                    remoteGrid.Rows.Clear();
                    AddRemoteBatch();
                });
            });
        }

        private void AddRemoteBatch()
        {
            int remaining = remoteEntries.Count - remoteDisplayedCount;
            int count = Math.Min(RemoteBatchSize, Math.Max(0, remaining));
            AppendGridRows(remoteGrid, remoteEntries, remoteDisplayedCount, count);
            remoteDisplayedCount += count;
            remoteMoreButton.Enabled = remoteDisplayedCount < remoteEntries.Count;
            remoteMoreButton.Text = remoteMoreButton.Enabled ? "更多" : "已全部";
            statusLabel.Text = remoteDisplayedCount < remoteEntries.Count
                ? "手机：已显示 " + remoteDisplayedCount + " / " + remoteEntries.Count + " 项 · " + remotePath
                : "手机：" + remoteEntries.Count + " 项 · " + remotePath;
        }

        private static List<FileEntry> ParseRemoteEntries(string output)
        {
            List<FileEntry> entries = new List<FileEntry>();
            string[] lines = (output ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            foreach (string line in lines)
            {
                if (line.Length == 0)
                    continue;
                string[] parts = line.Split(new char[] { '|' }, 4);
                if (parts.Length != 4)
                    continue;

                long size;
                double unixTime;
                if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
                    size = 0;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out unixTime))
                    unixTime = 0;

                string fullPath = parts[3];
                int slash = fullPath.LastIndexOf('/');
                string name = slash >= 0 ? fullPath.Substring(slash + 1) : fullPath;
                entries.Add(new FileEntry
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = parts[0].StartsWith("d", StringComparison.Ordinal),
                    Modified = epoch.AddSeconds(unixTime).ToLocalTime(),
                    Size = size
                });
            }

            entries.Sort(delegate(FileEntry left, FileEntry right)
            {
                if (left.IsDirectory != right.IsDirectory)
                    return left.IsDirectory ? -1 : 1;
                return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return entries;
        }

        private static void PopulateGrid(DataGridView grid, List<FileEntry> entries)
        {
            grid.Rows.Clear();
            AppendGridRows(grid, entries, 0, entries.Count);
        }

        private static void AppendGridRows(DataGridView grid, List<FileEntry> entries, int start, int count)
        {
            grid.SuspendLayout();
            int end = Math.Min(entries.Count, start + count);
            for (int index = start; index < end; index++)
            {
                FileEntry entry = entries[index];
                int rowIndex = grid.Rows.Add(
                    false,
                    entry.Name,
                    entry.Modified.ToString("yyyy-MM-dd HH:mm"),
                    entry.IsDirectory ? "文件夹" : FileType(entry.Name),
                    entry.IsDirectory ? "--" : FormatSize(entry.Size));
                grid.Rows[rowIndex].Tag = entry;
            }
            grid.ResumeLayout();
        }

        private static string FileType(string name)
        {
            string extension = Path.GetExtension(name);
            return extension.Length > 1 ? extension.Substring(1).ToUpperInvariant() : "文件";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024L * 1024L)
                return (bytes / 1024D).ToString("0.##") + " KB";
            if (bytes < 1024L * 1024L * 1024L)
                return (bytes / (1024D * 1024D)).ToString("0.##") + " MB";
            return (bytes / (1024D * 1024D * 1024D)).ToString("0.##") + " GB";
        }

        private void LocalGridDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            FileEntry entry = localGrid.Rows[e.RowIndex].Tag as FileEntry;
            if (entry != null && entry.IsDirectory)
                NavigateLocal(entry.FullPath);
        }

        private void RemoteGridDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            FileEntry entry = remoteGrid.Rows[e.RowIndex].Tag as FileEntry;
            if (entry != null && entry.IsDirectory)
                NavigateRemote(entry.FullPath);
        }

        private void BrowseLocal(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择电脑目录";
                dialog.SelectedPath = localPath;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    NavigateLocal(dialog.SelectedPath);
            }
        }

        private void CreateLocalFolder(object sender, EventArgs e)
        {
            string name = PromptDialog.Show(this, "新建电脑文件夹", "文件夹名称：");
            if (string.IsNullOrWhiteSpace(name))
                return;
            try
            {
                Directory.CreateDirectory(Path.Combine(localPath, name.Trim()));
                RefreshLocal();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "创建失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateRemoteFolder(object sender, EventArgs e)
        {
            string name = PromptDialog.Show(this, "新建手机文件夹", "文件夹名称：");
            if (string.IsNullOrWhiteSpace(name))
                return;

            string target = AdbClient.JoinRemotePath(remotePath, name.Trim());
            AdbResult result = adb.Shell("mkdir -p " + AdbClient.ShellQuote(target), 10000);
            if (!result.Success)
                MessageBox.Show(result.Message, "创建失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshRemote();
        }

        private List<FileEntry> GetChosenEntries(DataGridView grid)
        {
            grid.EndEdit();
            List<FileEntry> entries = new List<FileEntry>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                bool isChecked = row.Cells[0].Value is bool && (bool)row.Cells[0].Value;
                if (isChecked)
                {
                    FileEntry entry = row.Tag as FileEntry;
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    FileEntry entry = row.Tag as FileEntry;
                    if (entry != null && !entries.Contains(entry))
                        entries.Add(entry);
                }
            }
            return entries;
        }

        private void StartTransfer(bool toPhone)
        {
            if (transferWorker.IsBusy)
                return;

            List<FileEntry> selected = GetChosenEntries(toPhone ? localGrid : remoteGrid);
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "请勾选或选中至少一个文件/文件夹。",
                    "尚未选择",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int collisions = 0;
            foreach (FileEntry entry in selected)
            {
                string destination = toPhone
                    ? AdbClient.JoinRemotePath(remotePath, entry.Name)
                    : Path.Combine(localPath, entry.Name);
                bool exists = toPhone
                    ? adb.RemoteExists(destination)
                    : File.Exists(destination) || Directory.Exists(destination);
                if (exists)
                    collisions++;
            }

            bool overwrite = false;
            if (collisions > 0)
            {
                DialogResult choice = MessageBox.Show(
                    collisions + " 个目标已经存在。\n\n文件将覆盖，文件夹将合并同名内容。是否继续？",
                    "确认覆盖/合并",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (choice != DialogResult.Yes)
                    return;
                overwrite = true;
            }

            List<TransferJob> jobs = new List<TransferJob>();
            foreach (FileEntry entry in selected)
            {
                string destination = toPhone
                    ? AdbClient.JoinRemotePath(remotePath, entry.Name)
                    : Path.Combine(localPath, entry.Name);
                TransferJob job = new TransferJob
                {
                    ToPhone = toPhone,
                    Entry = entry,
                    Source = entry.FullPath,
                    DestinationDirectory = toPhone ? remotePath : localPath,
                    DestinationDisplay = destination,
                    Overwrite = overwrite
                };
                job.RowIndex = taskGrid.Rows.Add(
                    entry.Name,
                    toPhone ? "电脑 → 手机" : "手机 → 电脑",
                    "等待中",
                    entry.IsDirectory ? "文件夹" : FormatSize(entry.Size),
                    entry.FullPath,
                    destination,
                    "");
                jobs.Add(job);
            }

            SetBusy(true);
            transferWorker.RunWorkerAsync(jobs);
        }

        private void TransferWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            List<TransferJob> jobs = (List<TransferJob>)e.Argument;
            foreach (TransferJob job in jobs)
            {
                SetJobStatus(job, "传输中", "");
                job.Result = job.ToPhone
                    ? adb.PushPreservingName(
                        job.Source,
                        job.DestinationDirectory,
                        job.Entry.Name,
                        job.Entry.IsDirectory,
                        job.Overwrite)
                    : adb.PullPreservingName(
                        job.Source,
                        job.DestinationDirectory,
                        job.Entry.Name,
                        job.Entry.IsDirectory,
                        job.Overwrite);
                SetJobStatus(
                    job,
                    job.Result.Success ? "已完成" : "失败",
                    job.Result.Message);
            }
            e.Result = jobs;
        }

        private void SetJobStatus(TransferJob job, string status, string result)
        {
            if (IsDisposed)
                return;
            BeginInvoke((MethodInvoker)delegate
            {
                if (job.RowIndex < taskGrid.Rows.Count)
                {
                    DataGridViewRow row = taskGrid.Rows[job.RowIndex];
                    row.Cells[2].Value = status;
                    row.Cells[6].Value = result;
                    row.Cells[2].Style.ForeColor = status.IndexOf("完成", StringComparison.Ordinal) >= 0
                        ? success
                        : status == "失败" ? Color.Firebrick : text;
                }
                statusLabel.Text = job.Entry.Name + "：" + status;
            });
        }

        private void TransferWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            if (e.Error != null)
            {
                statusLabel.Text = "传输发生异常：" + e.Error.Message;
                return;
            }

            List<TransferJob> jobs = e.Result as List<TransferJob>;
            int failures = 0;
            if (jobs != null)
            {
                foreach (TransferJob job in jobs)
                {
                    if (job.Result == null || !job.Result.Success)
                        failures++;
                }
            }

            statusLabel.Text = failures == 0 ? "全部传输完成" : failures + " 个任务失败，请查看结果列";
            RefreshLocal();
            RefreshRemote();
        }

        private void SetBusy(bool busy)
        {
            sendButton.Enabled = !busy;
            receiveButton.Enabled = !busy;
            progress.Visible = busy;
        }
    }

    internal static class PromptDialog
    {
        public static string Show(IWin32Window owner, string title, string prompt)
        {
            using (Form form = new Form())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            using (Label label = new Label())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(390, 142);
                form.Font = new Font(UiTheme.FontFamily, 9F);
                form.BackColor = UiTheme.Background;
                form.ForeColor = UiTheme.Text;

                label.Text = prompt;
                label.ForeColor = UiTheme.Text;
                label.AutoSize = true;
                label.Location = new Point(18, 18);
                input.SetBounds(18, 45, 354, 28);
                input.BackColor = UiTheme.SurfaceMuted;
                input.ForeColor = UiTheme.Text;
                input.BorderStyle = BorderStyle.FixedSingle;
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(208, 92, 78, 32);
                ok.FlatStyle = FlatStyle.Flat;
                ok.BackColor = UiTheme.Accent;
                ok.ForeColor = Color.White;
                ok.FlatAppearance.BorderSize = 0;
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(294, 92, 78, 32);
                cancel.FlatStyle = FlatStyle.Flat;
                cancel.BackColor = UiTheme.Surface;
                cancel.ForeColor = UiTheme.Text;
                cancel.FlatAppearance.BorderColor = UiTheme.Border;

                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
