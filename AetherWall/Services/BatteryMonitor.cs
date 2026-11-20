using System;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;

namespace AetherWall.Services
{
    public class BatteryMonitor : IDisposable
    {
        private bool _isOnBattery;
        private bool _disposed;

        public bool IsOnBattery =>
            _isOnBattery;

        public event EventHandler<bool>? BatteryStateChanged;

        public BatteryMonitor()
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            System.Diagnostics.Debug.WriteLine(
                "[AetherWall] BatteryMonitor started");

            UpdateBatteryState();
        }

        private void OnPowerModeChanged(
            object? sender,
            PowerModeChangedEventArgs e)
        {
            if (_disposed)
                return;

            if (e.Mode != PowerModes.StatusChange &&
                e.Mode != PowerModes.Resume)
            {
                return;
            }

            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                UpdateBatteryState();
                return;
            }
            if (dispatcher.HasShutdownStarted ||
                dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (dispatcher.CheckAccess())
                UpdateBatteryState();
            else
                dispatcher.BeginInvoke(
                    new Action(UpdateBatteryState));
        }

        private void UpdateBatteryState()
        {
            PowerLineStatus powerStatus =
                SystemParameters.PowerLineStatus;

            bool onBattery =
                powerStatus ==
                PowerLineStatus.Offline;

            if (_isOnBattery == onBattery)
                return;

            _isOnBattery = onBattery;

            System.Diagnostics.Debug.WriteLine(
                onBattery
                    ? "[AetherWall] Battery power detected"
                    : "[AetherWall] AC power detected");

            BatteryStateChanged?.Invoke(
                this,
                onBattery);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            System.Diagnostics.Debug.WriteLine(
                "[AetherWall] BatteryMonitor stopped");
        }
    }
}
