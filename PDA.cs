using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using GHIElectronics.NETMF.System;

namespace MeterMateEMR3
{
    public class PDA
    {
        static object lockObj = new object();
        static SerialPort comPort;

        static byte STX = 0x02;
        static byte ETX = 0x03;

        #region Start communications

        public static void Start()
        {
            // Com3 is the serial port to the BlueTooth device.
            comPort = new SerialPort("COM3", 38400, Parity.None, 8, StopBits.One);
            comPort.ReadTimeout = 1000;
            comPort.WriteTimeout = 1000;
            comPort.Open();

            // Flush input buffer.
            byte[] buffer = new byte[1024];
            comPort.Read(buffer, 0, 1024);

            // Send Application Mode message.
            SendMessage("{\"Command\": \"AP\", \"Result\": 0 }");

            // Start background thread.
            Thread thread = new Thread(new ThreadStart(main));
            thread.Start();
        }

        #endregion

        #region Background thread

        public static void main()
        {
            // Input format: 0x02 CommandString {Comma Parameter1 {Comma Parameter2} } 0x03

            try
            {
                string message = string.Empty;
                byte[] byteIn = new byte[1];

                while (true)
                {
                    // Wait for input - this will timeout after 1 second.
                    if (comPort.Read(byteIn, 0, 1) > 0)
                    {
                        //Debug.Print("Byte in : " + (int)byteIn[0]);

                        // STX - Start of Text
                        if (byteIn[0] == STX)
                        {
                            // Start of message.
                            message = string.Empty;
                            continue;
                        }

                        // ETX - End of Text
                        if (byteIn[0] == ETX)
                        {
                            // End of message.
                            ProcessMessage(message);
                            continue;
                        }

                        // Add to message.
                        message += (char)byteIn[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("PDA.main: Exception " + ex.Message);
            }
        }

        #endregion

        #region Message processor

        public static void ProcessMessage(string message)
        {
            lock (lockObj)
            {
                try
                {
                    string json = "{\"Command\": \"\", \"Result\": -99}";
                    
                    Debug.Print("Received: " + message);

                    // Check EMR3 thread is running.
                    while (EMR3.IsRunning() == false)
                    {
                        Thread.Sleep(250);
                    }

                    // Check message is valid.
                    string[] parts = message.Split(new char[] { ',' });

                    if (parts.Length >= 1)
                    {
                        switch (parts[0])
                        {
                            case "BL":  // Bootloader.
                                SystemUpdate.AccessBootloader();
                                break;

                            case "Gv":  // Get Version.
                                json = "{\"Command\": \"Gv\", \"Result\": 0, \"Version\": " + Program.MajorVersion + "." + Program.MinorVersion + ", \"Model\": \"" + Program.Model + "\"}";
                                break;

                            case "Gf":  // Get features.
                                EMR3.GetFeatures(out json);
                                break;

                            case "Gt":  // Get temperature.
                                EMR3.GetTemperature(out json);
                                break;

                            case "Gs":  // Get status.
                                EMR3.GetStatus(out json);
                                break;

                            case "Gpl":  // Get preset litres.
                                EMR3.GetPreset(out json);
                                break;

                            case "Grl":  // Get realtime litres.
                                EMR3.GetRealtime(out json);
                                break;

                            case "Gtc":  // Get transaction count
                                EMR3.GetTranCount(out json);
                                break;

                            case "Gtr":  // Get transaction record
                                if (parts.Length == 2)
                                    EMR3.GetTran(parts[1], out json);
                                break;

                            case "Spl": // Set polling.
                                if (parts.Length == 2)
                                    EMR3.SetPolling(parts[1], out json);
                                break;

                            case "Sp":  // Set preset.
                                if (parts.Length == 2)
                                    EMR3.SetPreset(parts[1], out json);
                                break;

                            case "NOP":
                                json = "{\"Command\": \"NOP\", \"Result\": 0}";
                                break;

                            default:
                                json = "{\"Command\": \"" + parts[0] + "\", \"Result\": -99}";
                                break;
                        }
                    }

                    // Send reply to PDA.
                    SendMessage(json);

                    Debug.Print("PDA sent: " + json);
                }
                catch (Exception ex)
                {
                    Debug.Print("PDA.ProcessMessage: Exception " + ex.Message);
                }
            }
        }

        static void SendMessage(string json)
        {
            byte[] buffer = Encoding.UTF8.GetBytes((char)STX + json + (char)ETX);
            comPort.Write(buffer, 0, buffer.Length);
        }

        #endregion
    }
}
