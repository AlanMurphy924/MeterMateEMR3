using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT;

namespace MeterMateEMR3
{
    public class EMR3
    {
        static bool isRunning = false;
        static bool pollStatus = false;
        static SerialPort comPort;

        static bool InDeliveryMode = false;
        static bool ProductFlowing = false;
        static bool MeterError = false;
        static bool InCalibration = false;

        static byte delimiter = 0x7e;
        static byte escapeChar = 0x7d;
        static byte destination = 0x01;
        static byte source = 0xff;

        public static void Start()
        {
            // Com1 is the serial port to the Truck meter device.
            comPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
            comPort.ReadTimeout = 1000;
            comPort.WriteTimeout = 1000;
            comPort.Open();

            // Flush input buffer.
            byte[] buffer = new byte[1024];
            comPort.Read(buffer, 0, 1024);

            // Start background thread.
            Thread thread = new Thread(new ThreadStart(main));
            thread.Start();

            // Indicates startup is complete.
            isRunning = true;
        }

        static void main()
        {
            int idx = 0;

            while (true)
            {
                try
                {
                    //if (pollStatus)
                    {
                        // Poll meter for realtime litres.
                        PDA.ProcessMessage("Grl");

                        switch (idx)
                        {
                            case 0:
                                // Poll meter for status.
                                PDA.ProcessMessage("Gs");
                                break;

                            case 1:
                                // Poll meter for preset litres.
                                PDA.ProcessMessage("Gpl");
                                break;

                            case 2:
                                // Poll meter for current temperature.
                                PDA.ProcessMessage("Gt");
                                break;
                        }

                        if (++idx == 3)
                            idx = 0;
                    }
                    //else
                    {
                        // Restart at the top.
                        //idx = 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("EMR3: Exception " + ex.Message);
                }

                Thread.Sleep(100);
            }
        }

        public static bool IsRunning()
        {
            return isRunning;
        }

        #region Get features

        public static void GetFeatures(out string json)
        {
            json = "{\"Command\": \"Gf\", \"Result\": 0, \"Features\": \"Preset,Realtime\"}";
        }

        #endregion

        #region Get meter status

        /// <summary>
        /// Read the current meter status.
        /// </summary>
        public static void GetStatus(out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // T 0x01 - Get Meter status.
                byte[] message = new byte[2];
                message[0] = (byte)'T';
                message[1] = 0x01;

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    if (reply.Length == 3)
                    {
                        bool newInDeliveryMode = false;
                        bool newProductFlowing = false;

                        // Update Data class.
                        if ((reply[2] & 0x01) == 0x01)
                        {
                            newInDeliveryMode = false;
                            newProductFlowing = false;
                        }

                        if ((reply[2] & 0x02) == 0x02)
                        {
                            newInDeliveryMode = true;
                            newProductFlowing = true;
                        }

                        if ((reply[2] & 0x04) == 0x04)
                        {
                            newInDeliveryMode = true;
                            newProductFlowing = false;
                        }

                        if ((reply[2] & 0x08) == 0x08)
                        {
                            newInDeliveryMode = false;
                            newProductFlowing = true;
                        }

                        InDeliveryMode = newInDeliveryMode;
                        ProductFlowing = newProductFlowing;
                        MeterError = ((reply[2] & 0x40) == 0x40);
                        InCalibration = ((reply[2] & 0x80) == 0x80);

                        jsonBody = "\"Result\": 0, \"InDeliveryMode\": " + InDeliveryMode + ", \"ProductFlowing\": " + ProductFlowing + ", \"Error\": " + MeterError + ", \"InCalibration\": " + InCalibration;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetStatus: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Gs\", " + jsonBody + "}";
        }

        #endregion

        #region Get temperature

        /// <summary>
        /// Read the current temperature.
        /// </summary>
        public static void GetTemperature(out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // Gt - Get current temperature.
                byte[] message = new byte[2];
                message[0] = (byte)'G';
                message[1] = (byte)'t';

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    // Meter reply is Ft followed by 1 byte index.
                    if (reply[0] == 'F' && reply[1] == 't')
                    {
                        float tempF = BitConverter.ToSingle(reply, 2);
                        float tempC = (tempF - 32) * (5f / 9f);

                        jsonBody = "\"Result\": 0, \"Temp\": " + tempC.ToString("n1");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetProduct: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Gt\", " + jsonBody + "}";
        }

        #endregion

        #region Get preset litres

        /// <summary>
        /// Read the current preset litres.
        /// </summary>
        public static void GetPreset(out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // Gc - Get preset.
                byte[] message = new byte[2];
                message[0] = (byte)'G';
                message[1] = (byte)'c';

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    // Meter reply is Fc followed by 4 byte float.
                    if (reply[0] == 'F' && reply[1] == 'c')
                    {
                        jsonBody = "\"Result\": 0, \"Litres\": " + (int)BitConverter.ToSingle(reply, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetPreset: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Gpl\", " + jsonBody + "}";
        }

        #endregion

        #region Get realtime litres

        /// <summary>
        /// Read the current realtime litres.
        /// </summary>
        public static void GetRealtime(out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // Gc - Get preset.
                byte[] message = new byte[2];
                message[0] = (byte)'G';
                message[1] = (byte)'K';

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    // Meter reply is FK followed by 4 byte float.
                    if (reply[0] == 'F' && reply[1] == 'K')
                    {
                        jsonBody = "\"Result\": 0, \"Litres\": " + (int)BitConverter.ToDouble(reply, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetRealtime: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Grl\", " + jsonBody + "}";
        }

        #endregion

        #region Get transaction count

        /// <summary>
        /// Read the total transactions in meter.
        /// </summary>
        public static void GetTranCount(out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // H 0x00 - Get transaction count.
                byte[] message = new byte[2];
                message[0] = (byte)'H';
                message[1] = 0x00;

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    // Meter reply is I 0x00 followed by 2 byte counter.
                    if (reply[0] == 'I' && reply[1] == 0x00)
                    {
                        jsonBody = "\"Result\": 0, \"TranCount\": " + BitConverter.ToInt16(reply, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetTranCount: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Gtc\", " + jsonBody + "}";
        }

        #endregion

        #region Get transaction

        /// <summary>
        /// Read specified transaction.
        /// </summary>
        public static void GetTran(string tranNo, out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // H 0x00 - Get transaction.
                byte[] message = new byte[4];
                message[0] = (byte)'H';
                message[1] = 0x01;
                Utility.InsertValueIntoArray(message, 2, 2, Convert.ToUInt32(tranNo));

                // Send message to meter.
                byte[] reply = SendMessage(message);

                if (reply != null)
                {
                    // Meter reply is I 0x03 followed by 146 bytes of ticket data.
                    if (reply[0] == 'I' && reply[1] == 0x03)
                    {
                        // Build JSON string.
                        jsonBody = "\"Result\": 0, ";

                        // Ticket no.
                        uint ticketNo = Utility.ExtractValueFromArray(reply, 2, 4);
                        jsonBody += "\"TicketNo\":" + ticketNo + ",";

                        // Not sure about this stuff!
                        uint tranType = Utility.ExtractValueFromArray(reply, 6, 2);
                        jsonBody += "\"TranType\":" + tranType + ",";

                        uint index = Utility.ExtractValueFromArray(reply, 8, 1);
                        jsonBody += "\"Index\":" + index + ",";

                        uint noSummaryRecords = Utility.ExtractValueFromArray(reply, 9, 1);
                        jsonBody += "\"NoSummaryRecords\":" + noSummaryRecords + ",";

                        uint noRecordsSummarised = Utility.ExtractValueFromArray(reply, 10, 1);
                        jsonBody += "\"NoRecordsSummarised\":" + noRecordsSummarised + ",";

                        // Product ID.
                        uint productID = Utility.ExtractValueFromArray(reply, 11, 1);
                        jsonBody += "\"ProductID\":" + productID + ",";

                        // Product description.
                        byte[] product = Utility.ExtractRangeFromArray(reply, 12, 16);
                        string productDesc = new string(Encoding.UTF8.GetChars(product));
                        jsonBody += "\"ProductDesc\":\"" + productDesc + "\",";

                        // Start date/time.
                        uint startMinute = Utility.ExtractValueFromArray(reply, 28, 1);
                        uint startHour = Utility.ExtractValueFromArray(reply, 29, 1);
                        uint startDay = Utility.ExtractValueFromArray(reply, 30, 1);
                        uint startSecond = Utility.ExtractValueFromArray(reply, 31, 1);
                        uint startMonth = Utility.ExtractValueFromArray(reply, 32, 1);
                        uint startYear = Utility.ExtractValueFromArray(reply, 33, 1) + 2000;
                        jsonBody += "\"Start\":\"" + startDay + "/" + startMonth + "/" + startYear + " " + startHour + ":" + startMinute + ":" + startSecond + "\",";

                        // Finish date/time.
                        uint finishMinute = Utility.ExtractValueFromArray(reply, 34, 1);
                        uint finishHour = Utility.ExtractValueFromArray(reply, 35, 1);
                        uint finishDay = Utility.ExtractValueFromArray(reply, 36, 1);
                        uint finishSecond = Utility.ExtractValueFromArray(reply, 37, 1);
                        uint finishMonth = Utility.ExtractValueFromArray(reply, 38, 1);
                        uint finishYear = Utility.ExtractValueFromArray(reply, 39, 1) + 2000;
                        jsonBody += "\"Finish\":\"" + finishDay + "/" + finishMonth + "/" + finishYear + " " + finishHour + ":" + finishMinute + ":" + finishSecond + "\",";

                        // Totalisers.
                        double totaliserStart = BitConverter.ToDouble(reply, 48);
                        jsonBody += "\"totaliserStart\":" + totaliserStart + ",";

                        double totaliserEnd = BitConverter.ToDouble(reply, 56);
                        jsonBody += "\"totaliserEnd\":" + totaliserEnd + ",";

                        // Volume.
                        double grossVolume = BitConverter.ToDouble(reply, 64);
                        jsonBody += "\"grossVolume\":" + grossVolume + ",";

                        double volume = BitConverter.ToDouble(reply, 72);
                        jsonBody += "\"volume\":" + volume + ",";

                        // Temperature.
                        float tempF = BitConverter.ToSingle(reply, 80);
                        float tempC = (tempF - 32) * (5f / 9f);
                        jsonBody += "\"temperature\":" + tempC.ToString("n1") + ",";

                        // Flags.
                        uint flags = Utility.ExtractValueFromArray(reply, 126, 2);
                        jsonBody += "\"flags\":" + flags;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.GetTran: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Gtr\", " + jsonBody + "}";
        }

        #endregion

        #region Set polling

        /// <summary>
        /// Set polling mode.
        /// </summary>
        public static void SetPolling(string flag, out string json)
        {
            if (flag == "1")
                pollStatus = true;
            else
                pollStatus = false;

            json = "{\"Command\": \"Spl\", \"Result\": 0}";
        }

        #endregion

        #region Set preset

        /// <summary>
        /// Set preset to specified litres.
        /// </summary>
        public static void SetPreset(string litresString, out string json)
        {
            // Assume failure.
            string jsonBody = "\"Result\": -1";

            try
            {
                // Convert string to a float.
                float litres = 0;
                try { litres = Convert.ToInt32(litresString); }
                catch { }

                // Before presetting the meter, we press the MODE button 
                // until the device is in security mode. This is done to
                // ensure the meter is awake and after changing the preset
                // it helps to redraw the screen display too.
                byte[] reply2 = null;
                for (int i = 0; i < 3; i++)
                {
                    // Press MODE button.
                    byte[] message1 = new byte[3];
                    message1[0] = (byte)'S';
                    message1[1] = (byte)'u';
                    message1[2] = 0x02;
                    SendMessage(message1);

                    // Read meter display mode.
                    byte[] message2 = new byte[3];
                    message2[0] = (byte)'G';
                    message2[1] = (byte)'k';
                    message2[2] = 0x02;
                    reply2 = SendMessage(message2);

                    // Check if in SECURITY mode.
                    if (reply2 != null && reply2[2] == 0x03)
                        break;
                }

                if (reply2 != null && reply2[2] == 0x03)
                {
                    // Sc - Set preset.
                    byte[] message3 = new byte[6];
                    message3[0] = (byte)'S';
                    message3[1] = (byte)'c';
                    BitConverter.InsertValueIntoArray(message3, 2, litres);
                    byte[] reply = SendMessage(message3);

                    if (reply != null)
                    {
                        // Meter reply is A followed by:
                        //   0 - No error
                        //   1 - Error - requested code/action not understood
                        //   2 - Error - requested action can not be performed
                        if (reply[0] == 'A')
                            jsonBody = "\"Result\": " + (int)reply[1];

                        if (reply[1] == 0x00)
                        {
                            // Press MODE to switch back to volume mode.
                            byte[] message4 = new byte[3];
                            message4[0] = (byte)'S';
                            message4[1] = (byte)'u';
                            message4[2] = 0x02;
                            SendMessage(message4);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("EMR3.SetPreset: Exception " + ex.Message);
            }

            json = "{\"Command\": \"Sp\", " + jsonBody + "}";
        }

        #endregion

        #region EMR3 comms

        /// <summary>
        /// Send message to EMR3
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Reply from meter</returns>
        static byte[] SendMessage(byte[] message)
        {
            // EMR3 Format is:
            // delimiter, destination, source, message, checksum, delimiter

            byte[] reply = null;
            byte[] sendBuffer = new byte[1024];
            byte[] readBuffer = new byte[1024];

            int idx = 0;

            // Start message with delimited.
            sendBuffer[idx++] = delimiter;

            // Destination address.
            sendBuffer[idx++] = destination;

            // Source address.
            sendBuffer[idx++] = source;
        
            // Checksum is 0x100 - (destination + source + message)
            int chk = destination + source;
            for (int i = 0; i < message.Length; i++)
            {
                byte b = message[i];
             
                // Add to checksum.
                chk += b;

                // These characters must be 'escaped'
                if (b == escapeChar || b == delimiter)
                {
                    // Add escape character.
                    sendBuffer[idx++] = escapeChar;

                    // Xor with 0x20
                    b ^= 0x20;
                }

                sendBuffer[idx++] = b;
            }
    
            // Add checksum.
            sendBuffer[idx++] = (byte)(0x100 - (chk & 0xff));

            // Close message with delimiter.
            sendBuffer[idx++] = delimiter;

            // Send message to meter.
            if (comPort.Write(sendBuffer, 0, idx) == idx)
            {
                // Read reply from meter.
                int noBytes = comPort.Read(readBuffer, 0, 1024);
                int messageLen = noBytes - 5;

                if (messageLen > 0)
                {
                    // Deduct escape characters from message length.
                    for (int i = 3; i < noBytes - 2; i++)
                        if (readBuffer[i] == escapeChar)
                            messageLen--;

                    // Copy bytes to reply.
                    reply = new byte[messageLen];
                    for (int i = 0, j = 3; i < messageLen; i++, j++)
                    {
                        if (readBuffer[j] == escapeChar)
                            reply[i] = (byte)(readBuffer[++j] ^ 0x20);
                        else
                            reply[i] = readBuffer[j];
                    }
                }
            }

            return reply;
        }
    
        #endregion
    }
}
