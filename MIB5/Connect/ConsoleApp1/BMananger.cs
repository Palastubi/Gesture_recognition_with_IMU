using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Devices.Enumeration;

namespace ConsoleApp1
{
    public class BMananger
    {
        public IList<DeviceInformation> Devices = new List<DeviceInformation>();
        BWatcher watcher;

        public BMananger()
        {
            watcher = new BWatcher(new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" });
            watcher.Watcher.Added += OnNewDevice;
            watcher.Watcher.Updated += OnDeviceUpdated;
            watcher.Watcher.Removed += OnDeviceRemoved;
            watcher.Watcher.Start();
        }

        ~ BMananger()
        {
            if (watcher.Watcher != null)
            {
                watcher.Watcher.Added -= OnNewDevice;
                watcher.Watcher.Updated -= OnDeviceUpdated;
                watcher.Watcher.Removed -= OnDeviceRemoved;
                watcher.Watcher.Stop();
            }
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            IList<DeviceInformation> devs = Devices.Where(x => x.Id == args.Id).ToList();
            foreach (var d in devs)
            {
                Devices.Remove(d);
            }
        }

        private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            foreach (DeviceInformation d in Devices)
            {
                if (d.Id == args.Id)
                {
                    d.Update(args);
                    break;
                }
            }
        }

        private void OnNewDevice(DeviceWatcher sender, DeviceInformation args)
        {
            Devices.Add(args);
        }
    }
}
