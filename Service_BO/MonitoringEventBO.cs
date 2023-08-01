using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_BO
{
    public class MonitoringEventBO
    {
        public int ID { get; set; }
        public string RowNo { get; set; }
        public DateTime ServerPunchTime { get; set; }
        public DateTime PunchTime { get; set; }
        public string Index { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserPhotoExists { get; set; }
        public string UserGroupId { get; set; }
        public string UserGroupName { get; set; }
        public string DeviceSerialNo { get; set; }
        public string DeviceName { get; set; }
        public string EventCode { get; set; }
        public string tna_Key { get; set; }
        public string Image_ID_Data { get; set; }
        public string Image_ID_Type { get; set; }
        public string Image_ID_Photo { get; set; }
        public int is_dst { get; set; }
        public int TimeZone_half { get; set; }
        public int TimeZone_hour { get; set; }
        public int TimeZone_negative { get; set; }
        public string UserUpdatedByDevice { get; set; }
        public string Hint { get; set; }
        public string eventDescription { get; set; }
        public string cndt_NM { get; set; }
        public string JsonDataString { get; set; }
    }
}
