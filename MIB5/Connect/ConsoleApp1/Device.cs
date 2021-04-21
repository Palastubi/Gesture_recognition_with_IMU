using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace ConsoleApp1
{
    public class Device
    {
        private Guid AuthenticationSRVID = new Guid("0000fee1-0000-1000-8000-00805f9b34fb");
        private Guid AuthenticationCHARID = new Guid("00000009-0000-3512-2118-0009af100700");
        private Guid ActivityCHARID = new Guid("00000005-0000-3512-2118-0009af100700");
        private Guid HeartrateSRVID = new Guid("0000180d-0000-1000-8000-00805f9b34fb");
        private Guid HeartrateCHARID = new Guid("00002a39-0000-1000-8000-00805f9b34fb");
        private Guid HeartrateNOTIFYID = new Guid("00002a37-0000-1000-8000-00805f9b34fb");
        private Guid SensorSRVID = new Guid("0000fee0-0000-1000-8000-00805f9b34fb");
        private Guid SensorCHARID = new Guid("00000001-0000-3512-2118-0009af100700");
        private Guid SensorCHARMES = new Guid("00000002-0000-3512-2118-0009af100700");
        private string authKey;
        private BluetoothLEDevice bluetoothDevice;
        private GattDeviceService sensorService;
        private GattDeviceService heartrateService;
        private GattCharacteristic heartrateCharacteristic;
        private GattCharacteristic heartrateNCharacteristic;
        private GattCharacteristic sensorNCharacteristic;
        private Task HeartrateKeepAliveThread;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationToken ct;
        public string Id { get; private set; }
        public string Name { get; private set; }
        public DeviceStatus Status { get; private set; }
        public bool HeartrateMeasuringRuns { get; private set; }
        public ushort Heartrate { get; private set; }
        public Device(DeviceInformation deviceInformation, string AuthKey)
        {
            Id = deviceInformation.Id;
            Name = deviceInformation.Name;
            this.authKey = AuthKey;
            ct = cts.Token;
        }
        public void Authenticate()
        {
            var task = Task.Run(async () =>
            {
                GattDeviceServicesResult service = await bluetoothDevice.GetGattServicesForUuidAsync(AuthenticationSRVID);

                if (service.Status == GattCommunicationStatus.Success && service.Services.Count > 0)
                {
                    GattCharacteristicsResult gattCharacteristics = await service.Services[0].GetCharacteristicsForUuidAsync(AuthenticationCHARID);

                    if (gattCharacteristics.Status == GattCommunicationStatus.Success && gattCharacteristics.Characteristics.Count > 0)
                    {
                        GattCommunicationStatus notify = await gattCharacteristics.Characteristics[0].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                        if (notify == GattCommunicationStatus.Success)
                        {
                            gattCharacteristics.Characteristics[0].ValueChanged += Authenticated;
                            WriteToDevice(gattCharacteristics.Characteristics[0], new byte[] { 0x02, 0x00 });
                        }
                    }
                }
            });
        }

        void Authenticated(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] characteristic = new byte[3];

            using (DataReader reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(characteristic);
                if (characteristic[1] == 0x01)
                {
                    if (characteristic[2] == 0x01)
                    {
                        WriteToDevice(sender, new byte[] { 0x02, 0x08 });
                    }
                    else
                    {
                        throw new Exception("Authentication failed: " + characteristic[1].ToString() + " " + characteristic[2].ToString());
                    }
                }
                else if (characteristic[1] == 0x02)
                {
                    byte[] length = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(length);
                    using (var stream = new MemoryStream())
                    {
                        stream.Write(new byte[] { 0x03, 0x00 }, 0, 2);

                        byte[] encryptedNumber = Encryptor(length);
                        stream.Write(encryptedNumber, 0, encryptedNumber.Length);

                        WriteToDevice(sender, stream.ToArray());
                    }
                }
                else if (characteristic[1] == 0x03)
                {
                    if (characteristic[2] == 0x01)
                    {
                        Status = DeviceStatus.Authenticated;
                    }
                    else
                    {
                        throw new Exception("Authentication failed: " + characteristic[1].ToString() + " " + characteristic[2].ToString());
                    }
                }
            }
        }
        public void Connect()
        {
            bluetoothDevice = Task.Run(async () => await BluetoothLEDevice.FromIdAsync(Id)).Result;
            Status = DeviceStatus.UnAuthenticated;
        }

        async public void WriteToDevice(GattCharacteristic gattCharacteristic, byte[] data)
        {
            using (var stream = new DataWriter())
            {
                stream.WriteBytes(data);
                GattCommunicationStatus gattCommunication = await gattCharacteristic.WriteValueAsync(stream.DetachBuffer());
                if (gattCommunication > 0)
                {
                    throw new Exception("Write FAILED " + gattCommunication.ToString());
                }
            }
        }

        private byte[] Encryptor(byte[] secret)
        {
            byte[] message;

            using (Aes aes = Aes.Create())
            {
                aes.Key = ToByte(authKey);
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                using (MemoryStream stream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(secret, 0, secret.Length);
                        cryptoStream.FlushFinalBlock();
                        message = stream.ToArray();
                    }
                }
            }

            return message;
        }

        public static byte[] ToByte(string key)
        {
            byte[] bytes = new byte[key.Length / 2];
            for (int i = 0; i < key.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(key.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="continuous">Please write true if you want real time information.</param>
        public void StartHeartrate(bool continuous = false)
        {
            if (HeartrateMeasuringRuns)
                return;

            Task task = Task.Run(async () =>
            {
                GattCharacteristic sensorCharacteristic = null;
                GattDeviceServicesResult sensorServiceResult = await bluetoothDevice.GetGattServicesForUuidAsync(SensorSRVID);

                if (sensorServiceResult.Status == GattCommunicationStatus.Success && sensorServiceResult.Services.Count > 0)
                {
                    sensorService = sensorServiceResult.Services[0];
                    GattCharacteristicsResult characteristic = await sensorService.GetCharacteristicsForUuidAsync(SensorCHARID);
                    if (characteristic.Status == GattCommunicationStatus.Success && characteristic.Characteristics.Count > 0)
                    {
                        sensorCharacteristic = characteristic.Characteristics[0];
                        WriteToDevice(sensorCharacteristic, new byte[] { 0x01, 0x03, 0x19 });
                    }
                }

                GattDeviceServicesResult heartrateService = await bluetoothDevice.GetGattServicesForUuidAsync(HeartrateSRVID);

                if (heartrateService.Status == GattCommunicationStatus.Success && heartrateService.Services.Count > 0)
                {
                    this.heartrateService = heartrateService.Services[0];
                    GattCharacteristicsResult heartrateNotifyCharacteristic = await this.heartrateService.GetCharacteristicsForUuidAsync(HeartrateNOTIFYID);

                    if (heartrateNotifyCharacteristic.Status == GattCommunicationStatus.Success && heartrateNotifyCharacteristic.Characteristics.Count > 0)
                    {
                        GattCommunicationStatus notify = await heartrateNotifyCharacteristic.Characteristics[0].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                        if (notify == GattCommunicationStatus.Success)
                        {
                            heartrateNCharacteristic = heartrateNotifyCharacteristic.Characteristics[0];
                            heartrateNCharacteristic.ValueChanged += OnHeartrateRecived;
                        }
                    }

                    GattCharacteristicsResult heartrateResult = await this.heartrateService.GetCharacteristicsForUuidAsync(HeartrateCHARID);

                    if (heartrateResult.Status == GattCommunicationStatus.Success && heartrateResult.Characteristics.Count > 0)
                    {
                        heartrateCharacteristic = heartrateResult.Characteristics[0];

                        if (continuous)
                        {
                            WriteToDevice(heartrateCharacteristic, new byte[] { 0x15, 0x01, 0x01 });
                            HeartrateKeepAliveThread = new Task(RunHeartrateKeepAlive, TaskCreationOptions.LongRunning);
                            HeartrateKeepAliveThread.Start();
                        }
                        else
                        {
                            WriteToDevice(heartrateCharacteristic, new byte[] { 0x15, 0x02, 0x01 });
                        }

                        if (sensorCharacteristic != null)
                        {
                            WriteToDevice(sensorCharacteristic, new byte[] { 0x02 });
                        }
                    }
                }
                HeartrateMeasuringRuns = true;
            });
        }

        public void StopHeartrate()
        {
            if (!HeartrateMeasuringRuns)
                return;

            cts.Cancel();
            heartrateService?.Dispose();
            sensorService?.Dispose();

            if (heartrateCharacteristic != null)
            {
                WriteToDevice(heartrateCharacteristic, new byte[] { 0x15, 0x01, 0x00 });
                WriteToDevice(heartrateCharacteristic, new byte[] { 0x15, 0x02, 0x00 });
            }

            HeartrateMeasuringRuns = false;
        }


        void OnHeartrateRecived(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            using DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
            ushort value = reader.ReadUInt16();

            if (value > 0)
            {
                Heartrate = value;
            }

            if (HeartrateKeepAliveThread?.Status != TaskStatus.Running)
            {
                StopHeartrate();
            }
        }


        void RunHeartrateKeepAlive()
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                while (heartrateCharacteristic != null)
                {
                    WriteToDevice(heartrateCharacteristic, new byte[] { 0x16 });
                    Thread.Sleep(10000);
                }
            }
            catch (ThreadAbortException) { }
        }

        public void GetSteps()
        {
            Task task = Task.Run(async () =>
            {
                GattDeviceServicesResult sensorServiceResult = await bluetoothDevice.GetGattServicesForUuidAsync(SensorSRVID);
                GattCharacteristicsResult characteristicsResult = await sensorServiceResult.Services[0].GetCharacteristicsForUuidAsync(new Guid("00000007-0000-3512-2118-0009af100700"));
                GattReadResult readResult = await characteristicsResult.Characteristics[0].ReadValueAsync();
                using DataReader reader = DataReader.FromBuffer(readResult.Value);
                byte[] vs = new byte[10];
                reader.ReadBytes(vs);
                uint steps = reader.ReadUInt32();
                Console.WriteLine(steps.ToString().Substring(1,3));
                Console.WriteLine(steps);
                Console.WriteLine(steps.ToString().Substring(2, 4));
            });

        }

        public void GetDataFromAccelero()
        {
            Task task = Task.Run(async () =>
            {
                GattDeviceServicesResult sensorServiceResult = await bluetoothDevice.GetGattServicesForUuidAsync(SensorSRVID);
                GattDeviceServicesResult heartrateService = await bluetoothDevice.GetGattServicesForUuidAsync(HeartrateSRVID);
                GattCharacteristicsResult characteristicsResult1 = await sensorServiceResult.Services[0].GetCharacteristicsForUuidAsync(SensorCHARMES);
                GattCharacteristicsResult characteristicsResult2 = await sensorServiceResult.Services[0].GetCharacteristicsForUuidAsync(SensorCHARID);
                GattCharacteristicsResult characteristicsResult3 = await sensorServiceResult.Services[0].GetCharacteristicsForUuidAsync(new Guid("000000070000351221180009af100700"));
                GattCharacteristicsResult heartrateNotifyCharacteristic = await heartrateService.Services[0].GetCharacteristicsForUuidAsync(HeartrateNOTIFYID);
                if (characteristicsResult1.Status == GattCommunicationStatus.Success && heartrateNotifyCharacteristic.Characteristics.Count > 0)
                {
                    GattCommunicationStatus notify = await heartrateNotifyCharacteristic.Characteristics[0].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (notify == GattCommunicationStatus.Success)
                    {
                        sensorNCharacteristic = heartrateNotifyCharacteristic.Characteristics[0];
                        sensorNCharacteristic.ValueChanged += OnAccelData;
                    }
                }
                WriteToDevice(characteristicsResult2.Characteristics[0], new byte[] { 0x01, 0x03, 0x19 });
                GattDescriptorsResult descriptor = await heartrateNotifyCharacteristic.Characteristics[0].GetDescriptorsForUuidAsync(new Guid("0000" + "2902" + "-0000-1000-8000-00805f9b34fb"));
                DataWriter dataWriter = new DataWriter();
                dataWriter.WriteBytes(new byte[] { 1, 0 });
                await descriptor.Descriptors[0].WriteValueAsync(dataWriter.DetachBuffer());
                WriteToDevice(characteristicsResult2.Characteristics[0], new byte[] { 0x02 });
                HeartrateKeepAliveThread = new Task(RunHeartrateKeepAlive2, TaskCreationOptions.LongRunning);
                HeartrateKeepAliveThread.Start();
                while (true)
                {
                    ;
                }
            });
        }
        void OnAccelData(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            using DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
            reader.ReadByte();
            Console.WriteLine(reader.ReadByte());

            /*if (HeartrateKeepAliveThread?.Status != TaskStatus.Running)
            {
                StopHeartrate();
            }*/
        }

        void RunHeartrateKeepAlive2()
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                while (sensorNCharacteristic != null)
                {
                    WriteToDevice(sensorNCharacteristic, new byte[] { 0x16 });
                    Thread.Sleep(10000);
                }
            }
            catch (ThreadAbortException) { }
        }
    }
}

