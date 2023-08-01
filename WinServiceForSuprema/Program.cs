using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WinServiceForSuprema
{
    static class Program
    {
        public static bool IsMainTimerBusy = false;
        public static bool IsSecondTimerBusy = false;
        public static bool IsThirdTimerBusy = false;
        public static bool IsFourthTimerBusy = false;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
        }

    }
}
