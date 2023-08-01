using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_BO
{
    public class DeviceLogBO
    {
        public int ID { get; set; }
        public string LogDate { get; set; }
        public string DeviceSerialNo { get; set; }
        public string DeviceName { get; set; }
        public int DeviceStatus { get; set; }
    }
}
