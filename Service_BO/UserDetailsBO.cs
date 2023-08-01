using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_BO
{
    public class UserDetailsBO
    {
        public int ID { get; set; }
        public int? UserId { get; set; }
        public int Flag { get; set; }
        public int AccessRights_Id { get; set; }
        public DateTime Grnt_DT { get; set; }
        public DateTime? Grnt_Procd { get; set; }
        public DateTime? Rvk_DT { get; set; }
        public DateTime? Rvk_Procd { get; set; }
        public string Comment { get; set; }

        //Below feilds are added for employee creation/update
        public int? userGroup_Id { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string EmpName { get; set; }
        public string EmailId { get; set; }
        public int p_flag { get; set; }
    }
}
