using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Service_Common;
using System.Configuration;
using Service_BO;
using System.Data.SqlClient;
using System.Data;
using System.Web.Script.Serialization;
using Oracle.ManagedDataAccess.Client;
using Newtonsoft.Json;
using System.IO;
using System.Globalization;

namespace Service_BL
{
    public class MonitoringEvent
    {
        private readonly string connString = Encryption.Decrypt_Static(ConfigurationManager.ConnectionStrings["DBContextForOracle"].ToString());
        private string LogFolderPath = (ConfigurationManager.AppSettings["LogFolderPath"]);

        #region Insert function
        /// <summary>
        /// Farheen
        /// Description: Insert record.
        /// Created Date: 16 June'23
        /// </summary>
        /// <param name="model"></param>

        //[Obsolete]
        public ResponseMessageBO InsertMonitoringLog(MonitoringEventBO model, Dictionary<string, string> dictEventDesc)
        {
            ResponseMessageBO response = new ResponseMessageBO();
            try
            {
                using (OracleConnection con = new OracleConnection(connString))
                {
                    var json = new JavaScriptSerializer();
                    dynamic data = JsonConvert.DeserializeObject(model.JsonDataString);
                    var Count = data["EventCollection"]["rows"].Count;

                    List<MonitoringEventBO> itemDetails = new List<MonitoringEventBO>();
                    List<MonitoringEventBO> resultList = new List<MonitoringEventBO>();
                    string key = "";
                    for (int i = 0; i < (Count); i++)
                    {
                        MonitoringEventBO objItemDetails = new MonitoringEventBO();
                        try
                        {
                            string utcTimeString = data.EventCollection.rows[i].datetime;
                            DateTime utcDateTime = (DateTime.ParseExact(utcTimeString, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture)).ToLocalTime();

                            string serverTimeString = data.EventCollection.rows[i].server_datetime;
                            DateTime serverTime = (DateTime.ParseExact(serverTimeString, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture));

                            key = data.EventCollection.rows[i].event_type_id.code;
                            if (!FilterForSearch(key))
                                continue;

                            string eventDesc = dictEventDesc[key];
                            string accDenResult = SetResultAsPerEventCode(key);

                            var tnaKey = (data.EventCollection.rows[i].tna_key == null) ? "" : data.EventCollection.rows[i].tna_key;
                            var imagedata = (data.EventCollection.rows[i].image_id == null) ? "" : ((data.EventCollection.rows[i].image_id.image_data == null) ? "" : (data.EventCollection.rows[i].image_id.image_data));
                            var imageType = (data.EventCollection.rows[i].image_id == null) ? "" : ((data.EventCollection.rows[i].image_id.image_type == null) ? "" : (data.EventCollection.rows[i].image_id.image_type));
                            var imagePhoto = (data.EventCollection.rows[i].image_id == null) ? "" : ((data.EventCollection.rows[i].image_id.photo == null) ? "" : (data.EventCollection.rows[i].image_id.photo));

                            objItemDetails.RowNo = data.EventCollection.rows[i].id;
                            objItemDetails.ServerPunchTime = serverTime;
                            objItemDetails.PunchTime = utcDateTime;
                            objItemDetails.Index = data.EventCollection.rows[i].index;
                            objItemDetails.UserId = (data.EventCollection.rows[i].user_id.user_id) == null ? "0" : (data.EventCollection.rows[i].user_id.user_id);
                            objItemDetails.UserName = (data.EventCollection.rows[i].user_id.name) == null ? "" : (data.EventCollection.rows[i].user_id.name);
                            objItemDetails.UserPhotoExists = (data.EventCollection.rows[i].user_id.photo_exists) == null ? "" : (data.EventCollection.rows[i].user_id.photo_exists);
                            objItemDetails.UserGroupId = (data.EventCollection.rows[i].user_group_id.id) == null ? "" : (data.EventCollection.rows[i].user_group_id.id);
                            objItemDetails.UserGroupName = (data.EventCollection.rows[i].user_group_id.name) == null ? "" : (data.EventCollection.rows[i].user_group_id.name);
                            objItemDetails.DeviceSerialNo = data.EventCollection.rows[i].device_id.id;
                            objItemDetails.DeviceName = data.EventCollection.rows[i].device_id.name;
                            objItemDetails.EventCode = data.EventCollection.rows[i].event_type_id.code;
                            objItemDetails.tna_Key = tnaKey;
                            objItemDetails.Image_ID_Data = imagedata;
                            objItemDetails.Image_ID_Type = imageType;
                            objItemDetails.Image_ID_Photo = imagePhoto;
                            objItemDetails.is_dst = (data.EventCollection.rows[i].is_dst) == null ? 0 : (data.EventCollection.rows[i].is_dst);
                            objItemDetails.TimeZone_half = (data.EventCollection.rows[i].timezone.half) == null ? 0 : (data.EventCollection.rows[i].timezone.half);
                            objItemDetails.TimeZone_hour = (data.EventCollection.rows[i].timezone.hour) == null ? 0 : (data.EventCollection.rows[i].timezone.hour);
                            objItemDetails.TimeZone_negative = (data.EventCollection.rows[i].timezone.negative) == null ? 0 : (data.EventCollection.rows[i].timezone.negative);
                            objItemDetails.UserUpdatedByDevice = (data.EventCollection.rows[i].user_update_by_device) == null ? "" : (data.EventCollection.rows[i].user_update_by_device);
                            objItemDetails.Hint = (data.EventCollection.rows[i].hint) == null ? "" : (data.EventCollection.rows[i].hint);
                            objItemDetails.eventDescription = eventDesc;
                            objItemDetails.cndt_NM = accDenResult;

                            itemDetails.Add(objItemDetails);
                        }
                        catch (Exception ex)
                        {
                            string pathForExcep = LogFolderPath + "ExceptionRecords.txt";
                            using (StreamWriter writer = new StreamWriter(pathForExcep, true))
                            {
                                writer.WriteLine("ExceptionRecords: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff") + "\n");
                                writer.WriteLine("Exception message: EventCode: " + key + "# " + ex.Message + "_#_" + ex.StackTrace + "_#_" + ex + "\n");
                                writer.WriteLine("RowNo: " + objItemDetails.RowNo + ", ServerPunchTime: " +
                                objItemDetails.ServerPunchTime + ", Punch_Time: " + objItemDetails.PunchTime +
                                ", Indexing: " + objItemDetails.Index + ", UserId: " + objItemDetails.UserId +
                                ", User_Name: " + objItemDetails.UserName + ", DeviceSerialNo: " + objItemDetails.DeviceSerialNo +
                                ", DeviceName: " + objItemDetails.DeviceName + ", EventCode: " + objItemDetails.EventCode +
                                ", is_dst:" + objItemDetails.is_dst +
                                ", TimeZone_half:" + objItemDetails.TimeZone_half + ", TimeZone_hour:" +
                                objItemDetails.TimeZone_hour +
                                ", TimeZone_negative:" + objItemDetails.TimeZone_negative +
                                ", UserUpdatedByDevice:" + objItemDetails.UserUpdatedByDevice +
                                ", Hint:" + objItemDetails.Hint +
                                "\n");
                                writer.Close();
                            }
                            continue;
                            throw;
                        }
                    }

                    foreach (var item in itemDetails)
                    {
                        try
                        {
                            con.Open();
                            OracleCommand cmdNew = new OracleCommand("usp_tbl_MonitoringEvent_Insert", con);
                            cmdNew.CommandType = CommandType.StoredProcedure;

                            cmdNew.Parameters.Add("row_no", item.RowNo);
                            cmdNew.Parameters.Add("ServerPunchTime", item.ServerPunchTime);
                            cmdNew.Parameters.Add("Punch_Time", item.PunchTime);
                            cmdNew.Parameters.Add("Indexing", item.Index);
                            cmdNew.Parameters.Add("UserId", item.UserId);
                            cmdNew.Parameters.Add("User_Name", item.UserName);
                            cmdNew.Parameters.Add("UserPhotoExists", item.UserPhotoExists);
                            cmdNew.Parameters.Add("UserGroupId", item.UserGroupId);
                            cmdNew.Parameters.Add("UserGroupName", item.UserGroupName);
                            cmdNew.Parameters.Add("DeviceSerialNo", item.DeviceSerialNo);
                            cmdNew.Parameters.Add("DeviceName", item.DeviceName);
                            cmdNew.Parameters.Add("EventCode", item.EventCode);
                            cmdNew.Parameters.Add("tna_Key", item.tna_Key);
                            cmdNew.Parameters.Add("Image_ID_Data", item.Image_ID_Data);
                            cmdNew.Parameters.Add("Image_ID_Type", item.Image_ID_Type);
                            cmdNew.Parameters.Add("Image_ID_Photo", item.Image_ID_Photo);
                            cmdNew.Parameters.Add("is_dst", item.is_dst);
                            cmdNew.Parameters.Add("TimeZone_half", item.TimeZone_half);
                            cmdNew.Parameters.Add("TimeZone_hour", item.TimeZone_hour);
                            cmdNew.Parameters.Add("TimeZone_negative", item.TimeZone_negative);
                            cmdNew.Parameters.Add("UserUpdatedByDevice", item.UserUpdatedByDevice);
                            cmdNew.Parameters.Add("Hint", item.Hint);
                            cmdNew.Parameters.Add("EventDescription", item.eventDescription);
                            cmdNew.Parameters.Add("CNDT_NM", item.cndt_NM);
                            // cmdNew.Parameters.Add("Status", OracleDbType.Int64).Direction = ParameterDirection.Output;
                            OracleParameter Param1 = cmdNew.Parameters.Add("Status", OracleDbType.Decimal, 0);
                            Param1.Direction = ParameterDirection.Output;
                            //Param1.CollectionType = OracleCollectionType.None;

                            cmdNew.ExecuteNonQuery();

                            var res = Param1.Value;

                            if (res.ToString().Equals("0"))
                            {
                                resultList.Add(item);
                            }

                            response.Status = true;
                            con.Close();
                        }
                        catch (Exception ex)
                        {
                            string pathForExcep = LogFolderPath + "ExceptionRecords.txt";
                            using (StreamWriter writer = new StreamWriter(pathForExcep, true))
                            {
                                writer.WriteLine("DBConnection_Exception_Records: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff") + "\n");
                                writer.WriteLine("Exception message:" + ex.Message + "_#_" + ex.StackTrace + "\n");
                                writer.WriteLine("RowNo: " + item.RowNo + ", ServerPunchTime: " +
                                item.ServerPunchTime + ", Punch_Time: " + item.PunchTime +
                                ", Indexing: " + item.Index + ", UserId: " + item.UserId +
                                ", User_Name: " + item.UserName + ", UserPhotoExists: " + item.UserPhotoExists +
                                ", UserGroupId: " + item.UserGroupId + ", UserGroupName: " + item.UserGroupName +
                                ", DeviceSerialNo: " + item.DeviceSerialNo + ", DeviceName: " + item.DeviceName +
                                ", EventCode: " + item.EventCode + ", tna_Key:" + item.tna_Key +
                                ", Image_ID_Data:" + item.Image_ID_Data + ", Image_ID_Type:" + item.Image_ID_Type +
                                ", Image_ID_Photo:" + item.Image_ID_Photo + ", is_dst:" + item.is_dst +
                                ", TimeZone_half:" + item.TimeZone_half + ", TimeZone_hour:" + item.TimeZone_hour +
                                ", TimeZone_negative:" + item.TimeZone_negative +
                                ", UserUpdatedByDevice:" + item.UserUpdatedByDevice + ", Hint:" + item.Hint +
                                "\n");
                                writer.Close();
                            }
                            con.Close();
                            continue;
                            throw;
                        }

                    }

                    //string path = LogFolderPath + "DuplicateData.txt";
                    //int itemLen = resultList.Count;
                    //int j = 0;
                    //if (itemLen > 0)
                    //{
                    //    using (StreamWriter writer = new StreamWriter(path, true))
                    //    {
                    //        writer.WriteLine("DuplicateData: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    //        while (j < itemLen)
                    //        {
                    //            writer.WriteLine((j + 1) + ". RowNo: " + resultList[j].RowNo + ", ServerPunchTime: " +
                    //                resultList[j].ServerPunchTime + ", Punch_Time: " + resultList[j].PunchTime +
                    //                ", Indexing: " + resultList[j].Index + ", UserId: " + resultList[j].UserId +
                    //                ", User_Name: " + resultList[j].UserName + ", UserPhotoExists: " + resultList[j].UserPhotoExists +
                    //                ", UserGroupId: " + resultList[j].UserGroupId + ", UserGroupName: " + resultList[j].UserGroupName +
                    //                ", DeviceSerialNo: " + resultList[j].DeviceSerialNo + ", DeviceName: " + resultList[j].DeviceName +
                    //                ", EventCode: " + resultList[j].EventCode + ", tna_Key:" + resultList[j].tna_Key +
                    //                ", Image_ID_Data:" + resultList[j].Image_ID_Data + ", Image_ID_Type:" + resultList[j].Image_ID_Type +
                    //                ", Image_ID_Photo:" + resultList[j].Image_ID_Photo + ", is_dst:" + resultList[j].is_dst +
                    //                ", TimeZone_half:" + resultList[j].TimeZone_half + ", TimeZone_hour:" + resultList[j].TimeZone_hour +
                    //                ", TimeZone_negative:" + resultList[j].TimeZone_negative +
                    //                ", UserUpdatedByDevice:" + resultList[j].UserUpdatedByDevice + ", Hint:" + resultList[j].Hint +
                    //                "\n");
                    //            j++;
                    //        }
                    //        writer.Close();
                    //    }

                    //}

                }

            }
            catch (Exception ex)
            {
                response.Status = false;
                response.ExceptionMessage = ex.Message.ToString();
            }
            return response;
        }

        /// <summary>
        /// Created By: Farheen
        /// Created Date: 14 July'23
        /// Description: This function is use to check the access, wheather it is granted
        /// or failed for the perticular event code.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <returns></returns>
        public string SetResultAsPerEventCode(string eventCode)
        {
            string result;

            string grantEvent = ",4096,4097,4098,4099,4100,4101,4102,4103,4104,4105,4106,4107,4112,4113,4114," +
                "4115,4118,4119,4120,4121,4122,4123,4128,4129,4864,4865,4866,4867,4868,4869,4870,4871,4872," +
                "5632,6912,6913,6914,6915,";
            string deniedEvent = ",4352,4353,4354,4355,4356,4357,4360,4361,5120,5123,5124,5125,5888,5889," +
                "5890,6144,6145,6146,6147,6148,6410,6411,6656,7168,7169,7170,6400,6401,6402,6403,6404,6405,6406,6407,6414,6415,6418,6419,6420,6421,";

            eventCode = ',' + eventCode + ',';
            if (grantEvent.Contains(eventCode))
                result = "Grant";
            else if (deniedEvent.Contains(eventCode))
                result = "Fail";
            else
                result = "Undefied";

            return result;
        }

        /// <summary>
        /// Created By: Farheen
        /// Created Date: 14 July'23
        /// Description: This function is for applying filter on the searched response i.e.
        /// only events which mentioned in the filterCode strig, must get inserted 
        /// in the database.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <returns></returns>
        public bool FilterForSearch(string eventCode)
        {

            string filterCode = ",4096,4097,4098,4099,4100,4101,4102,4103,4104,4105,4106,4107," +
                "4112,4113,4114,4115,4118,4119,4120,4121,4122,4123,4128,4129,4864,4865,4866,4867," +
                "4868,4869,4870,4871,4872,5632,6912,6913,6914,6915,4352,4353,4354,4355,4356," +
                "4357,4360,4361,5120,5123,5124,5125,5888,5889,5890,6144,6145,6146,6147,6148,6410," +
                "6411,6656,7168,7169,7170,6400,6401,6402,6403,6404,6405,6406,6407,6414,6415,6418,6419,6420,6421,";

            eventCode = ',' + eventCode + ',';

            if (filterCode.Contains(eventCode))
                return true;
            else
                return false;
        }

        #endregion

        #region Fetch list of user for updating AG
        public List<UserDetailsBO> GetUserDetails()
        {
            List<UserDetailsBO> resultList = new List<UserDetailsBO>();
            try
            {
                using (OracleConnection con = new OracleConnection(connString))
                {
                    using (OracleCommand command = new OracleCommand("usp_tbl_AccessRights_get", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        OracleParameter outputParameter = new OracleParameter();
                        outputParameter.ParameterName = "out_cursor";
                        outputParameter.OracleDbType = OracleDbType.RefCursor;
                        outputParameter.Direction = ParameterDirection.Output;
                        command.Parameters.Add(outputParameter);

                        con.Open();
                        using (OracleDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UserDetailsBO result = new UserDetailsBO();
                                result.ID = reader.GetInt32(reader.GetOrdinal("IDN"));
                                result.UserId = reader.GetInt32(reader.GetOrdinal("EMPID"));
                                result.AccessRights_Id = reader.GetInt32(reader.GetOrdinal("ACCESS_GRP_ID"));
                                result.Flag = reader.GetInt32(reader.GetOrdinal("op_type"));
                                result.Grnt_DT = reader.GetDateTime(reader.GetOrdinal("dt"));
                                
                                //result.Grnt_DT = reader.GetDateTime(reader.GetOrdinal("grnt_dt"));

                                //int ProcdOrdinal = reader.GetOrdinal("grnt_procd");
                                //if (!reader.IsDBNull(ProcdOrdinal))
                                //    result.Grnt_Procd = reader.GetDateTime(ProcdOrdinal);
                                //else
                                //    result.Grnt_Procd = null;

                                //ProcdOrdinal = reader.GetOrdinal("rvk_dt");
                                //if (!reader.IsDBNull(ProcdOrdinal))
                                //    result.Rvk_DT = reader.GetDateTime(ProcdOrdinal);
                                //else
                                //    result.Rvk_DT = null;

                                ////result.Rvk_DT = reader.GetDateTime(reader.GetOrdinal("rvk_dt"));

                                //ProcdOrdinal = reader.GetOrdinal("rvk_procd");
                                //if (!reader.IsDBNull(ProcdOrdinal))
                                //    result.Rvk_Procd = reader.GetDateTime(ProcdOrdinal);
                                //else
                                //    result.Rvk_Procd = null;


                                resultList.Add(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from BL fetch AG details: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }
            
            return resultList;
        }

        public ResponseMessageBO UpdateFlag_AG(UserDetailsBO model)
        {
            ResponseMessageBO response = new ResponseMessageBO();
            try
            {
                using (OracleConnection con = new OracleConnection(connString))
                {
                    con.Open();
                    OracleCommand cmd = new OracleCommand("usp_tbl_AccessRights_Update", con);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("row_ID", model.ID);
                    cmd.Parameters.Add("grnt_pdt", model.Grnt_Procd);
                    cmd.Parameters.Add("rvk_pdt", model.Rvk_Procd);
                    cmd.Parameters.Add("flag", model.Flag);
                    cmd.Parameters.Add("comment", model.Comment);

                    OracleParameter Param1 = cmd.Parameters.Add("Status", OracleDbType.Decimal, 0);
                    Param1.Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();
                    var res = Param1.Value;

                    if (res.ToString().Equals("1"))
                        response.Status = true;
                    else
                        response.Status = false;

                    con.Close();
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.ExceptionMessage = ex.Message + "\n" + ex + "\n" + ex.StackTrace;
                throw;
            }

            return response;
        }
        #endregion

        #region Fnctions for fetching list of employees and update the table
        public List<UserDetailsBO> GetEmployeeDetails()
        {
            List<UserDetailsBO> resultList = new List<UserDetailsBO>();
            try
            {
                using (OracleConnection con = new OracleConnection(connString))
                {
                    using (OracleCommand command = new OracleCommand("usp_tbl_EmployeeDummy_get", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        OracleParameter outputParameter = new OracleParameter();
                        outputParameter.ParameterName = "out_cursor";
                        outputParameter.OracleDbType = OracleDbType.RefCursor;
                        outputParameter.Direction = ParameterDirection.Output;
                        command.Parameters.Add(outputParameter);

                        con.Open();
                        using (OracleDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UserDetailsBO result = new UserDetailsBO();
                                result.ID = reader.GetInt32(reader.GetOrdinal("ID"));
                                result.UserId = reader.GetInt32(reader.GetOrdinal("USERID"));
                                result.userGroup_Id = reader.GetInt32(reader.GetOrdinal("user_grp_id"));

                                int ordinal = reader.GetOrdinal("start_dt");
                                if (!reader.IsDBNull(ordinal))
                                    result.startDate = reader.GetDateTime(ordinal);
                                else
                                    result.startDate = null;

                                ordinal = reader.GetOrdinal("expiry_dt");
                                if (!reader.IsDBNull(ordinal))
                                    result.ExpiryDate = reader.GetDateTime(ordinal);
                                else
                                    result.ExpiryDate = null;

                                ordinal = reader.GetOrdinal("name");
                                if (!reader.IsDBNull(ordinal))
                                    result.EmpName = reader.GetString(ordinal);
                                else
                                    result.EmpName = null;

                                ordinal = reader.GetOrdinal("email_id");
                                if (!reader.IsDBNull(ordinal))
                                    result.EmailId = reader.GetString(ordinal);
                                else
                                    result.EmailId = null;

                                result.Flag = reader.GetInt32(reader.GetOrdinal("flag"));
                                result.p_flag = reader.GetInt32(reader.GetOrdinal("p_flag"));

                                resultList.Add(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from BL fetch AG details: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }

            return resultList;
        }

        public ResponseMessageBO UpdateEmployeeTable(UserDetailsBO model)
        {
            ResponseMessageBO response = new ResponseMessageBO();
            try
            {
                using (OracleConnection con = new OracleConnection(connString))
                {
                    con.Open();
                    OracleCommand cmd = new OracleCommand("usp_tbl_EmployeeDummy_Update", con);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("Row_id", model.ID);
                    cmd.Parameters.Add("Pro_flag", model.p_flag);

                    OracleParameter Param1 = cmd.Parameters.Add("Status", OracleDbType.Decimal, 0);
                    Param1.Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();
                    var res = Param1.Value;

                    if (res.ToString().Equals("1"))
                        response.Status = true;
                    else
                        response.Status = false;

                    con.Close();
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.ExceptionMessage = ex.Message + " " + ex.StackTrace;
                throw;
            }

            return response;
        }
        #endregion
    }
}