using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AetherWallSetup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox installPath = new();
    private readonly CheckBox desktopShortcut = new();
    private readonly Button installButton = new();
    private readonly ProgressBar progress = new();
    private readonly Label status = new();

    public SetupForm()
    {
        Text = "AetherWall Setup";
        ClientSize = new Size(540, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "Install AetherWall",
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = true,
            Location = new Point(28, 24)
        };
        var description = new Label
        {
            Text = "Choose where to install the app. It will be installed for your Windows account.",
            AutoSize = false,
            Size = new Size(480, 42),
            Location = new Point(30, 68)
        };
        var pathLabel = new Label { Text = "Install location", AutoSize = true, Location = new Point(30, 124) };
        installPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AetherWall");
        installPath.Location = new Point(30, 148);
        installPath.Size = new Size(390, 27);
        var browseButton = new Button { Text = "Browse…", Location = new Point(430, 146), Size = new Size(80, 30) };
        browseButton.Click += BrowseForFolder;

        desktopShortcut.Text = "Create a desktop shortcut";
        desktopShortcut.Checked = true;
        desktopShortcut.AutoSize = true;
        desktopShortcut.Location = new Point(30, 194);

        status.Text = "No changes will be made until you select Install.";
        status.AutoSize = false;
        status.Size = new Size(480, 22);
        status.Location = new Point(30, 232);

        progress.Location = new Point(30, 258);
        progress.Size = new Size(480, 12);
        progress.Style = ProgressBarStyle.Continuous;
        progress.Visible = false;

        var cancelButton = new Button { Text = "Cancel", Location = new Point(342, 278), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        installButton.Text = "Install";
        installButton.Location = new Point(430, 278);
        installButton.Size = new Size(80, 30);
        installButton.Click += Install;

        Controls.AddRange([title, description, pathLabel, installPath, browseButton, desktopShortcut, status, progress, cancelButton, installButton]);
        AcceptButton = installButton;
        CancelButton = cancelButton;
    }

    private void BrowseForFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose an installation folder for AetherWall",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(installPath.Text) ? installPath.Text : Path.GetDirectoryName(installPath.Text)!
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            installPath.Text = Path.Combine(dialog.SelectedPath, "AetherWall");
        }
    }

    private async void Install(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(installPath.Text))
        {
            MessageBox.Show(this, "Choose an installation folder first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var destination = Path.GetFullPath(installPath.Text.Trim());

        if (Process.GetProcessesByName("AetherWall").Length > 0)
        {
            MessageBox.Show(this, "Close AetherWall, then run Setup again.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        installButton.Enabled = false;
        desktopShortcut.Enabled = false;
        installPath.Enabled = false;
        progress.Visible = true;
        progress.Value = 0;
        status.Text = "Installing AetherWall…";

        try
        {
            var progressReporter = new Progress<int>(value => progress.Value = Math.Clamp(value, 0, 100));
            await Task.Run(() => CopyApplication(destination, progressReporter));
            CreateShortcutsAndUninstallEntry(destination, desktopShortcut.Checked);

            var launch = MessageBox.Show(this, "AetherWall was installed successfully. Launch it now?", "Setup complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (launch == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(Path.Combine(destination, "AetherWall.exe")) { UseShellExecute = true, WorkingDirectory = destination });
            }
            Close();
        }
        catch (Exception exception)
        {
            status.Text = "Installation could not be completed.";
            installButton.Enabled = true;
            desktopShortcut.Enabled = true;
            installPath.Enabled = true;
            MessageBox.Show(this, $"AetherWall could not be installed.\r\n\r\n{exception.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CopyApplication(string destination, IProgress<int> progressReporter)
    {
        Directory.CreateDirectory(destination);
        var appPath = Path.Combine(destination, "AetherWall.exe");
        var uninstallerPath = Path.Combine(destination, "Uninstall-AetherWall.ps1");

        using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("AetherWall.Setup.Payload.exe")
            ?? throw new InvalidOperationException("The AetherWall application payload is missing."))
        using (var output = new FileStream(appPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copied += read;
                progressReporter.Report((int)(copied * 100 / input.Length));
            }
        }

        using var uninstallSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AetherWall.Setup.Uninstaller.ps1")
            ?? throw new InvalidOperationException("The uninstaller file is missing.");
        using var uninstallOutput = new FileStream(uninstallerPath, FileMode.Create, FileAccess.Write, FileShare.None);
        uninstallSource.CopyTo(uninstallOutput);

        using var noticesSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AetherWall.Setup.ThirdPartyNotices.txt")
            ?? throw new InvalidOperationException("The third-party notices are missing.");
        using var noticesOutput = new FileStream(Path.Combine(destination, "THIRD-PARTY-NOTICES.txt"), FileMode.Create, FileAccess.Write, FileShare.None);
        noticesSource.CopyTo(noticesOutput);
    }

    private static void CreateShortcutsAndUninstallEntry(string installDirectory, bool addDesktopShortcut)
    {
        var appPath = Path.Combine(installDirectory, "AetherWall.exe");
        var startMenuDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AetherWall");
        Directory.CreateDirectory(startMenuDirectory);
        CreateShortcut(Path.Combine(startMenuDirectory, "AetherWall.lnk"), appPath, installDirectory);
        if (addDesktopShortcut)
        {
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AetherWall.lnk"), appPath, installDirectory);
        }

        const string uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AetherWall";
        using var key = Registry.CurrentUser.CreateSubKey(uninstallKey, writable: true)
            ?? throw new InvalidOperationException("Could not register AetherWall in Windows app settings.");
        key.SetValue("DisplayName", "AetherWall");
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "Naveen");
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("DisplayIcon", appPath);
        key.SetValue("UninstallString", $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(installDirectory, "Uninstall-AetherWall.ps1")}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(string shortcutPath, string appPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows could not create the AetherWall shortcut.");
        var shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [shortcutPath])!;
            shortcut.TargetPath = appPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = $"{appPath},0";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
