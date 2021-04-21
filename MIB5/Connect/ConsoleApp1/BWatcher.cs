using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace ConsoleApp1
{
    public class BWatcher
    {
        private DeviceWatcher watcher;
        public EventHandler WatcherChanged;
        public DeviceWatcher Watcher { get { return watcher; } set { watcher = value; WatcherChanged?.Invoke(this, new EventArgs()); } }

        public BWatcher(string[] additionalProperties)
        {
            Watcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), additionalProperties, DeviceInformationKind.AssociationEndpoint);
        }

        ~BWatcher()
        {
            Watcher?.Stop();
        }

    }
}
