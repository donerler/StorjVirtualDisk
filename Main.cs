using Dokan;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StorjVirtualDisk
{
    public class Main : ApplicationContext
    {
        //Component declarations
        private NotifyIcon TrayIcon;
        private ContextMenuStrip TrayIconContextMenu;
        private ToolStripMenuItem CloseMenuItem; 
        
        private Task dokanTask;
        private Lazy<char> driveLetter = new Lazy<char>(() => GetAvailableDrive());

        public Main()
        {
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeComponent();
            TrayIcon.Visible = true;
            
            DokanOptions opt = new DokanOptions();
            opt.MountPoint = driveLetter.Value.ToString();
            opt.VolumeLabel = "Storj";

            dokanTask =  Task.Factory.StartNew(() => DokanNet.DokanMain(opt, new StorjOperations(OnCommunication)));

            TrayIcon.BalloonTipTitle = "Storj Virtual Disk ready";
            TrayIcon.BalloonTipText = "The Storj Virtual Disk successfully mounted to drive '" + driveLetter + "' and is now ready to use.";
            TrayIcon.ShowBalloonTip(TimeSpan.FromSeconds(5).Milliseconds);
        }

        private void OnCommunication(bool isBussy)
        {
            TrayIcon.Icon = isBussy ? Properties.Resources.TrayIconRefresh : Properties.Resources.TrayIcon;
        }

        private void InitializeComponent()
        {
            TrayIcon = new NotifyIcon();
            TrayIcon.BalloonTipIcon = ToolTipIcon.Info;
            TrayIcon.Text = "STORJ Virtual Disk";
            TrayIcon.Icon = Properties.Resources.TrayIcon;

            TrayIconContextMenu = new ContextMenuStrip();
            CloseMenuItem = new ToolStripMenuItem();
            TrayIconContextMenu.SuspendLayout();

            // 
            // TrayIconContextMenu
            // 
            this.TrayIconContextMenu.Items.AddRange(new ToolStripItem[] {
            this.CloseMenuItem});
            this.TrayIconContextMenu.Name = "TrayIconContextMenu";
            this.TrayIconContextMenu.Size = new Size(153, 70);

            // 
            // CloseMenuItem
            // 
            this.CloseMenuItem.Name = "CloseMenuItem";
            this.CloseMenuItem.Size = new Size(152, 22);
            this.CloseMenuItem.Text = "Exit";
            this.CloseMenuItem.Click += new EventHandler(this.CloseMenuItem_Click);

            TrayIconContextMenu.ResumeLayout(false);
            TrayIcon.ContextMenuStrip = TrayIconContextMenu;
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            //Cleanup so that the icon will be removed when the application is closed
            TrayIcon.Visible = false;

            DokanNet.DokanUnmount(driveLetter.Value);
            driveLetter = null;
        }

        private void CloseMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you realy want to exit STORJ Virtual Disk?",
                    "Exit STORJ Virtual Disk", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }
        
        private static char GetAvailableDrive()
        {
            const string driveLetters = "CDEFGHIJKLMNOPQRSTUVWXYZ";

            return driveLetters.Except(DriveInfo.GetDrives().Select(drive => drive.Name.First())).FirstOrDefault();
        }
    }
}
