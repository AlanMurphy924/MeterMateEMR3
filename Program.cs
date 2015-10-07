using System;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.System;

//
// Version history
// ===============
//
// 1.0 : 28-08-12 : KD : Initial version.
// 1.1 : 30-08-12 : KD : Changed all return values to JSON format.
// 1.2 : 06-09-12 : KD : System Update support via Bluetooth port.
// 1.3 : 17-01-13 : KD : Added GetTemperature, improved EMR3 set preset, improved EMR3 polling.
//

namespace MeterMateEMR3
{
    public class Program
    {
        public const int MajorVersion = 1;
        public const int MinorVersion = 3;
        public const string Model = "EMR3";

        public static void Main()
        {
            bool switchToBootLoader = false;

            var mode = SystemUpdate.GetMode();

            if (mode == SystemUpdate.SystemUpdateMode.NonFormatted)
            {
                // This erases the application!
                Debug.Print("SystemUpdate.EnableBootLoader - formatting !!!!!!");
                SystemUpdate.EnableBootloader();
            }

            if (mode == SystemUpdate.SystemUpdateMode.Bootloader)
            {
                // Switch to application mode.
                Debug.Print("SystemUpdate.AccessApplication");
                SystemUpdate.AccessApplication();
            }

            if (mode == SystemUpdate.SystemUpdateMode.Application)
            {
                // Developer use only: Switch to BootLoader
                if (switchToBootLoader)
                    SystemUpdate.AccessBootloader();
            }

            // LED - Flashing for 5 seconds.
            LED.State = LED.LedState.FlashingData;
            Thread.Sleep(5000);

            // LED - switch on.
            LED.State = LED.LedState.On;

            // Start PDA comms thread.
            PDA.Start();

            // Start meter comms thread.
            EMR3.Start();

            while (true)
            {
                // Sleep a second
                Thread.Sleep(1000);
            }
        }
    }
}
