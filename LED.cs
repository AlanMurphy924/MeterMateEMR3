using System.Threading;

using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;

namespace MeterMateEMR3
{
    class LED
    {
        static OutputPort led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, false);

        #region Constructor

        static LED()
        {
            // Start background thread to manage LED.
            Thread ledThread = new Thread(new ThreadStart(ledMain));
            ledThread.Start();
        }

        #endregion

        #region Properties

        public enum LedState
        {
            Off,
            FlashingData,
            FlashingError,
            On
        }

        static LedState state = LedState.Off;
        /// <summary>
        /// Controls state of LED
        /// i.e. off, on or flashing.
        /// </summary>
        public static LedState State
        {
            get { return state; }
            set { state = value; }
        }

        #endregion

        #region Thread

        static void ledMain()
        {
            bool ledState = false;
            int sleepTime = 1000;

            while (true)
            {
                switch (state)
                {
                    case LedState.Off:
                        ledState = false;
                        sleepTime = 1000;
                        break;

                    case LedState.FlashingData:
                        ledState = !ledState;
                        sleepTime = 500;
                        break;

                    case LedState.FlashingError:
                        ledState = !ledState;
                        sleepTime = 50;
                        break;

                    case LedState.On:
                        ledState = true;
                        sleepTime = 1000;
                        break;
                }

                led.Write(ledState);
                Thread.Sleep(sleepTime);
            }
        }

        #endregion
    }
}
