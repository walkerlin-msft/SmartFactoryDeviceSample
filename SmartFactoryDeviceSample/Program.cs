using CSSDK;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryDeviceSample
{
    class Program
    {
        static SfDeviceClient _sfDeviceClient;

        static void Main(string[] args)
        {
            string DEVICE_ID = ConfigurationManager.AppSettings["Device.Id"];
            string DEVICE_PASSWORD = ConfigurationManager.AppSettings["Device.Password"];
            string CERTIFICATE_PATH = ConfigurationManager.AppSettings["CertificatePath"];// It can be null or empty if the x509 certificate did not be used.
            string LOG_FILE_NAME = ConfigurationManager.AppSettings["LogFileName"];

            try
            {
                HwProductKey hwProductKey = HwProductKey.CreateHwProductKey(DEVICE_ID, DEVICE_PASSWORD);

                /* Create the instance of SfDeviceClient */
                _sfDeviceClient = SfDeviceClient.CreateSfDeviceClient(hwProductKey, CERTIFICATE_PATH);

                /* initialzation */
                _sfDeviceClient.Initial().Wait();

                /* Async Task for the telemetry data sending */
                SendTelemetryBySchemaAsync();

                /* Async Task for the Cloud to Device message receiving */
                ReceiveCloudToDeviceMessageAsync();

                /* Async Task for the log uploading*/
                UploadLogAsync(LOG_FILE_NAME);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("InnerException: {0}", ex.InnerException.Message);
                    Console.WriteLine("InnerException StackTrace: {0}", ex.InnerException.StackTrace);
                }
            }

            Console.ReadLine();
        }

        static async void SendTelemetryBySchemaAsync()
        {
            int companyId = [Please put your COMPANY ID here...]; // You can get the company id from JSON template            

            int messageCount = 5;
            try
            {
                while (messageCount > 0)
                {
                    Random rand = new Random();
                    int seed = rand.Next() % 2;

                    string equipmentId = [Please put your EQUIPMENT ID here...]; //getSampleEquipmentId(seed); // As an example, one or more equipments can be bound in the one device
                    int messageCatalogId = [Please put your MESSAGE CATALOG ID here...]; //getSampleMessageCataId(seed);// As an example, specify the message ID what you want to sent

                    string deviceMessage = getSampleDeviceMessage(companyId, equipmentId, messageCatalogId);

                    await _sfDeviceClient.SendEventAsyncWithRetry(messageCatalogId, deviceMessage);

                    Console.WriteLine("{0} > Message Sending: {1}", DateTime.Now, deviceMessage);
                    messageCount--;

                    Task.Delay(5000).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendAsync Exception: {0}", ex.ToString());
            }
        }

        static string getSampleEquipmentId(int seed)
        {
            switch (seed)
            {
                case 0:
                    return "MachineTool-2f";
                case 1:
                default:
                    return "InjectionMachine-2f";
            }
        }

        static int getSampleMessageCataId(int seed)
        {
            switch (seed)
            {
                case 0:
                    return 45;// MachineTool-TypeA
                case 1:
                default:
                    return 46; // InjectionMachine-TypeA
            }
        }

        static string getSampleDeviceMessage(int companyId, string equipmentId, int messageCatalogId)
        {
            JObject deviceMessage = new JObject();
            deviceMessage.Add("companyId", companyId);
            deviceMessage.Add("msgTimestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            deviceMessage.Add("equipmentId", equipmentId);
            deviceMessage.Add("equipmentRunStatus", 1);

            switch (messageCatalogId)
            {
                case 46:
                    deviceMessage.Add("orderNumber", "ORDER_123456");
                    deviceMessage.Add("RPM-expectd", 5400);
                    Random rand = new Random();
                    int seed = rand.Next() % 101;
                    deviceMessage.Add("RPM-actual", 5350+ seed);
                    deviceMessage.Add("CoolingSystemWarning", false);// Optional, it can be ignored
                    break;
                case 45:
                    deviceMessage.Add("machineOnOff", true);
                    deviceMessage.Add("orderId", "ID12345");
                    deviceMessage.Add("temperature", 23.1);
                    deviceMessage.Add("RPM", 7200);
                    deviceMessage.Add("bootingInfo_startTime", DateTime.Now.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss"));
                    deviceMessage.Add("bootingInfo_endTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                    deviceMessage.Add("bootingInfo_materialStock", 999999);
                    break;
            }

            return deviceMessage.ToString();

        }

        static async void ReceiveCloudToDeviceMessageAsync()
        {
            while (true)
            {
                Message receivedMessage = await _sfDeviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;// It returns null after a specifiable timeout period (in this case, the default of one minute is used)

                string msg = Encoding.ASCII.GetString(receivedMessage.GetBytes());

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Received message: {0}\n", msg);
                Console.ResetColor();

                await _sfDeviceClient.CompleteAsync(receivedMessage);
            }
        }

        static async void UploadLogAsync(string fileName)
        {
            Console.WriteLine("UploadLogAsync: fileName={0}", fileName);

            FileInfo fi = new FileInfo(fileName);
            if (fi.Exists)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                await _sfDeviceClient.uploadLog(fi);

                sw.Stop();
                Console.WriteLine("{0} has been uploaded in {1} ms", fi.Name, sw.ElapsedMilliseconds);
            }
            else
            {
                Console.WriteLine("UPLOADED LOG FILE NOT FOUND. fileName={0}", fileName);
            }
        }
    }
}
