﻿using PasteEx.Core;
using PasteEx.Util;
using System;
using System.IO;
using System.Windows.Forms;

namespace PasteEx.Forms
{
    public partial class FormMain : Form
    {
        #region Init

        private static FormMain dialogue = null;

        private ClipboardData data;

        private string currentLocation;

        private string lastAutoGeneratedFileName;

        public string CurrentLocation
        {
            get
            {
                return currentLocation;
            }
            set
            {
                currentLocation = value.EndsWith("\\") ? value : value + "\\";
                tsslCurrentLocation.ToolTipText = currentLocation;
                tsslCurrentLocation.Text = PathGenerator.GenerateDisplayLocation(currentLocation);
            }
        }

        public static FormMain GetInstance()
        {
            return dialogue;
        }

        public FormMain()
        {
            dialogue = this;
            InitializeComponent();
            CurrentLocation = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        public FormMain(string location)
        {
            dialogue = this;
            InitializeComponent();

            if (location == null)
            {
                // Switch to monitor mode
                Load -= FormMain_Load;
                Load += FormMain_Monitor_Load;
                Visible = false;
                StartMonitorModeUI();
            }
            else
            {
                CurrentLocation = location;
            }
        }
        private void FormMain_Monitor_Load(object sender, EventArgs e)
        {
            this.BeginInvoke(new Action(() =>
            {
                this.Hide(); // hide main form
                notifyIcon.ShowBalloonTip(1000, Resources.Strings.TitleAppName, Resources.Strings.TipMonitorImHere, ToolTipIcon.None);
            }));
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            data = new ClipboardData();
            data.SaveCompleted += () => Application.Exit(); // exit when save completed
            string[] extensions = data.Analyze();
            cboExtension.Items.AddRange(extensions);
            if (extensions.Length > 0)
            {
                cboExtension.Text = extensions[0] ?? "";
            }
            else
            {
                if (MessageBox.Show(this, Resources.Strings.TipAnalyzeFailed, Resources.Strings.Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    btnChooseLocation.Enabled = false;
                    btnSave.Enabled = false;
                    txtFileName.Enabled = false;
                    cboExtension.Enabled = false;
                    tsslCurrentLocation.Text = Resources.Strings.TxtCanOnlyUse;
                }
                else
                {
                    Environment.Exit(0);
                }

            }

            lastAutoGeneratedFileName = PathGenerator.GenerateFileName(CurrentLocation, cboExtension.Text);
            txtFileName.Text = lastAutoGeneratedFileName;
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            ModeController.UnregisterHotKey();
            ModeController.StopMonitorMode();
            CommandLine.CloseConsole();
        }
        #endregion

        #region UI event
        private void btnSave_Click(object sender, EventArgs e)
        {
            btnChooseLocation.Enabled = false;
            btnSettings.Enabled = false;
            btnSave.Enabled = false;

            string location = CurrentLocation.EndsWith("\\") ? CurrentLocation : CurrentLocation + "\\";
            string path = location + txtFileName.Text + "." + cboExtension.Text;

            if (File.Exists(path))
            {
                DialogResult result = MessageBox.Show(string.Format(Resources.Strings.TipTargetFileExisted, path),
                    Resources.Strings.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    data.SaveAsync(path, cboExtension.Text);
                }
                else if (result == DialogResult.No)
                {
                    btnChooseLocation.Enabled = true;
                    btnSettings.Enabled = true;
                    btnSave.Enabled = true;
                    return;
                }
            }
            else
            {
                data.SaveAsync(path, cboExtension.Text);
            }
        }

        private void btnChooseLocation_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show(this, Resources.Strings.TipPathNotNull,
                        Resources.Strings.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                else
                {
                    CurrentLocation = dialog.SelectedPath;
                }
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            Button btnSender = (Button)sender;
            System.Drawing.Point ptLowerLeft = new System.Drawing.Point(0, btnSender.Height);
            ptLowerLeft = btnSender.PointToScreen(ptLowerLeft);
            contextMenuStripSetting.Show(ptLowerLeft);


        }

        public void monitorModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Util.ApplicationHelper.IsPasteExMonitorModeProcessesExist())
            {
                MessageBox.Show(this, Resources.Strings.TipMonitorProcessExisted,
                        Resources.Strings.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Util.ApplicationHelper.StartSelf("monitor", false);
                this.Close();
            }
        }

        public void StartMonitorModeUI()
        {
            // dispose
            data = null;

            // init control properties
            autoToolStripMenuItem.Checked = Properties.Settings.Default.autoImageToFileEnabled;
            startMonitorToolStripMenuItem.Visible = false;
            stopMonitorToolStripMenuItem.Visible = true;

            // hide main window and display system tray icon
            dialogue.WindowState = FormWindowState.Minimized;
            dialogue.ShowInTaskbar = false;
            dialogue.Hide();
            dialogue.notifyIcon.Visible = true;

            try
            {
                ModeController.RegisterHotKey(Properties.Settings.Default.pasteHotkey);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message,
                        Resources.Strings.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            ModeController.StartMonitorMode();
        }

        private void settingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormSetting f = FormSetting.GetInstance();
            if (f.Visible == true)
            {
                f.Show();
            }
            else
            {
                f.ShowDialog();
            }
            f.Activate();
        }

        private void cboExtension_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Re-Generate FileName
            if (lastAutoGeneratedFileName == txtFileName.Text)
            {
                lastAutoGeneratedFileName = PathGenerator.GenerateFileName(CurrentLocation, cboExtension.Text);
                txtFileName.Text = lastAutoGeneratedFileName;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // (keyData == (Keys.Control | Keys.S))
            if (keyData == Keys.Enter)
            {
                btnSave_Click(null, null);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }


        public void ChangeTsslCurrentLocation(string str)
        {
            tsslCurrentLocation.Text = str;
        }

        private void autoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoToolStripMenuItem.Checked = !autoToolStripMenuItem.Checked;
            Properties.Settings.Default.autoImageToFileEnabled = autoToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            FormSetting f = FormSetting.GetInstance();
            f.Show();
            f.ChangeSelectedTabToModeTab();
            f.Activate();
        }

        private void startMonitorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipboardMonitor.Start();
            startMonitorToolStripMenuItem.Visible = false;
            stopMonitorToolStripMenuItem.Visible = true;
            notifyIcon.Icon = Properties.Resources.ico;
        }

        private void stopMonitorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipboardMonitor.Stop();
            startMonitorToolStripMenuItem.Visible = true;
            stopMonitorToolStripMenuItem.Visible = false;
            notifyIcon.Icon = Properties.Resources.stop;
        }

        private void openDebugWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommandLine.NewConsole();
            // banner
            CommandLine.WriteLine(
@" ____                       __               ____              
/\  _`\                    /\ \__           /\  _`\            
\ \ \L\ \   __       ____  \ \ ,_\     __   \ \ \L\_\   __  _  
 \ \ ,__/ /'__`\    /',__\  \ \ \/   /'__`\  \ \  _\L  /\ \/'\ 
  \ \ \/ /\ \L\.\_ /\__, `\  \ \ \_ /\  __/   \ \ \L\ \\/>  </ 
   \ \_\ \ \__/.\_\\/\____/   \ \__\\ \____\   \ \____/ /\_/\_\
    \/_/  \/__/\/_/ \/___/     \/__/ \/____/    \/___/  \//\/_/
                                                               
                                                               ");
            CommandLine.WriteLine(
"               * PasteEx v." + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " Debug Window");
            CommandLine.WriteLine("    * If you close this window, the application will also be closed." + Environment.NewLine);
        }

        #endregion

    }
}
