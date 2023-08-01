using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Configuration;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net;
using Service_BL;
using Service_BO;
using System.Timers;
using System.Globalization;
using Service_Common;
using System.Web.Script.Serialization;

namespace WinServiceForSuprema
{
    [RunInstaller(true)]
    public partial class Service1 : ServiceBase
    {
        #region Global variable declaration

        double ScheduleTime = Convert.ToDouble(ConfigurationManager.AppSettings["ElapseTime"]);
        int QueryLimit = Convert.ToInt32(ConfigurationManager.AppSettings["QueryLimit"]);
        static string Url = (ConfigurationManager.AppSettings["Url"]);
        static string PortNo = (ConfigurationManager.AppSettings["PortNo"]);
        static string LoginUrl = "https://" + Url + ":" + PortNo + "/api/login";
        static string AllAddedDeviceUrl = "https://" + Url + ":" + PortNo + "/api/devices";
        static string SearchUrl = "https://" + Url + ":" + PortNo + "/api/events/search";
        static string ViewTypeUrl = "https://" + Url + ":" + PortNo + "/api/event_types";
        static string UpdateAGUrl = "https://" + Url + ":" + PortNo + "/api/users";

        static string UserName = (ConfigurationManager.AppSettings["UserName"]);
        static string Password = Encryption.Decrypt_Static(ConfigurationManager.AppSettings["Password"].ToString());
        static string LogFolderPath = (ConfigurationManager.AppSettings["LogFolderPath"]);

        string responsebody_Log;
        string sessionID;

        private System.Timers.Timer timer;
        static int count;
        static string startDate = "";
        static string endDate = "";

        static bool FlagIfDeviceExist = false;

        //=======The below DBType is for fetching database type used by user from App.config file.====//
        static string DBType = (ConfigurationManager.AppSettings["DatabaseType"]);

        //======Declare a dictionary for storing the status of devices======//
        public static volatile Dictionary<string, DeviceLogBO> deviceStatus = new Dictionary<string, DeviceLogBO>();

        //======Declare a dictionary for storing the status of devices======//
        public static readonly Dictionary<string, string> dictionaryEventDesc = new Dictionary<string, string>();

        //The below variable is for sync lock on dictionary
        private static object lockObject = new object();

        volatile string sessionIdForThread;
        #endregion

        #region Initialize parameterless constructor
        public Service1()
        {
            InitializeComponent();
        }
        #endregion

        #region Start event of Service
        protected override void OnStart(string[] args)
        {
            try
            {
                SetEventDescription(); //This function is to set event description in dictionary.
                CheckStatusFileExist();

                //FunctionForOldEventSync(); //Fetch event date from logged file.
                Thread thread = new Thread(() => FunctionForOldEventSync());
                thread.Start();

                count = 0;
                count++; //Count is used for preventing to reset time to current, at every elapsed.

                this.timer = new System.Timers.Timer(ScheduleTime * 30 * 1000D);  // 30000 milliseconds = 30 seconds
                this.timer.AutoReset = true;
                this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Elapsed);
                this.timer.Start();

                this.timer = new System.Timers.Timer(ScheduleTime * 30 * 1000D);  // 30000 milliseconds = 30 seconds
                this.timer.AutoReset = true;
                this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timerElapsed_MultDevices);
                this.timer.Start();

                this.timer = new System.Timers.Timer(ScheduleTime * 50 * 1000D);  // 5 sec
                this.timer.AutoReset = true;
                this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timerElapsed_UpdateAccessGrp);
                this.timer.Start();

                //this.timer = new System.Timers.Timer(ScheduleTime * 60 * 1000D);  // 6 sec
                //this.timer.AutoReset = true;
                //this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timerElapsed_UpdateEmployee);
                //this.timer.Start();
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error Start: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }
        #endregion

        #region Timer Elapsed event
        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Program.IsMainTimerBusy)
                return;

            try
            {
                Program.IsMainTimerBusy = true;

                Working();
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from main timer: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }

            finally
            {
                //The below code will store the start and end datetime in separate file.
                var path = LogFolderPath + "ServiceDateLogs.txt";
                int dateTimeInterval = Convert.ToInt32(ConfigurationManager.AppSettings["DateTimeInterval"]);

                if (startDate == null || startDate == "")
                    startDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                if (endDate == null || endDate == "")
                    endDate = DateTime.UtcNow.AddSeconds(dateTimeInterval).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");

                string dateString = startDate + "," + endDate + "#" + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff");
                File.WriteAllText(path, dateString);

                Program.IsMainTimerBusy = false;
            }
        }

        private void timerElapsed_MultDevices(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Program.IsSecondTimerBusy)
                return;
            try
            {
                Program.IsSecondTimerBusy = true;
                //=========Checking if any added device switch the status event===========//
                CheckDeviceEvent();
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from device timer: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }
            finally
            {
                Program.IsSecondTimerBusy = false;
            }
        }

        private void timerElapsed_UpdateAccessGrp(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Program.IsThirdTimerBusy)
                return;
            try
            {
                Program.IsThirdTimerBusy = true;
                //=========Call function for fetching the list of access group
                //which need to be updated===========//
                if (DBType == "Oracle")
                {
                    MonitoringEvent objBL = new MonitoringEvent();
                    List<UserDetailsBO> userDetails = new List<UserDetailsBO>();

                    userDetails = objBL.GetUserDetails();
                    if (userDetails.Count > 0)
                    {
                        UpdateAccessGroup(userDetails);
                    }
                }
                else if (DBType == "SQLServer")
                {
                    MonitoringEventForSQL objSQLBL = new MonitoringEventForSQL();
                    List<UserDetailsBO> userDetails = new List<UserDetailsBO>();

                    userDetails = objSQLBL.GetUserDetails();
                    if (userDetails.Count > 0)
                    {
                        UpdateAccessGroup(userDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from third timer: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }
            finally
            {
                Program.IsThirdTimerBusy = false;
            }
        }

        private void timerElapsed_UpdateEmployee(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Program.IsFourthTimerBusy)
                return;
            try
            {
                Program.IsFourthTimerBusy = true;
                //=========Call function for fetching the list of access group
                //which need to be updated===========//
                if (DBType == "Oracle")
                {
                    MonitoringEvent objBL = new MonitoringEvent();
                    List<UserDetailsBO> userDetails = new List<UserDetailsBO>();

                    userDetails = objBL.GetEmployeeDetails();
                    if (userDetails.Count > 0)
                    {
                        UpdateEmployeeDetails(userDetails);
                    }
                }
                else if (DBType == "SQLServer")
                {
                    MonitoringEventForSQL objSQLBL = new MonitoringEventForSQL();
                    List<UserDetailsBO> userDetails = new List<UserDetailsBO>();

                    userDetails = objSQLBL.GetEmployeeDetails();
                    if (userDetails.Count > 0)
                    {
                        UpdateEmployeeDetails(userDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine("Exception from fourth timer: " + ex.Message + "_#_"
                        + ex + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    writer.Close();
                }
                throw;
            }
            finally
            {
                Program.IsFourthTimerBusy = false;
            }
        }

        #endregion

        #region Function for initiating old event thread.
        static void FunctionForOldEventSync()
        {
            string path = LogFolderPath + "Log.txt";

            //Check if the date log file exist or not and if do then fetch the date from there.

            string filePath = LogFolderPath + "ServiceDateLogs.txt";
            bool fileExists = File.Exists(filePath);
            Service1 obj = new Service1();
            //int dateTimeInterval = Convert.ToInt32(ConfigurationManager.AppSettings["DateTimeInterval"]);
            string tempEndDate, tempStartDate, CurrentDate;

            if (fileExists)
            {
                try
                {
                    string lastLoggedDate = obj.ReadLastDateFromFile(filePath);
                    string[] result = lastLoggedDate.Split(',');
                    tempStartDate = result[0];
                    tempEndDate = result[1].Split('#')[0];
                    CurrentDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                    startDate = CurrentDate;

                    var uri = LoginUrl;
                    var t = Task.Run(() => obj.SendPostRequest(uri, 1));
                    t.Wait(1 * 60 * 1000);

                    uri = SearchUrl;
                    var t1 = Task.Run(() => obj.SendPostRequestForOldEvent(uri, obj.sessionIdForThread, tempStartDate, tempEndDate, CurrentDate));
                    t1.Wait(1 * 60 * 1000);
                }
                catch (Exception ex)
                {
                    path = LogFolderPath + "Log.txt";
                    using (StreamWriter writer = new StreamWriter(path, true))
                    {
                        writer.WriteLine(string.Format("Date assignment Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                        writer.Close();
                    }
                    //tempStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                    //tempEndDate = DateTime.UtcNow.AddSeconds(dateTimeInterval).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                }
            }
        }

        #endregion

        #region Function for update/create employee
        static void UpdateEmployeeDetails(List<UserDetailsBO> list_objBO)
        {
            foreach (UserDetailsBO item in list_objBO)
            {
                Service1 obj = new Service1();
                var uri = LoginUrl;
                var t = Task.Run(() => obj.SendPostRequest(uri, 1));
                t.Wait(1 * 60 * 1000);

                uri = UpdateAGUrl;
                var t2 = Task.Run(() => obj.UpdateEmployee_API(uri, obj.sessionIdForThread, item));
                t2.Wait(1 * 60 * 1000);
            }

        }

        public async Task UpdateEmployee_API(string url, string sessionId, UserDetailsBO objBO)
        {
            string path;
            try
            {
                if (sessionId != null & sessionId != "")
                {
                    //Create Employee
                    HttpClient httpClient = new HttpClient();
                    if (objBO.Flag == 0)
                    {
                        if (objBO.UserId == null || objBO.UserId == 0)
                        {

                            string urlForUID = url + "/next_user_id";
                            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                            httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                            HttpResponseMessage response = await httpClient.GetAsync(urlForUID);
                            string responseForUid = await response.Content.ReadAsStringAsync();
                            if (response.IsSuccessStatusCode)
                            {
                                dynamic result = JsonConvert.DeserializeObject(responseForUid);
                                objBO.UserId = Convert.ToInt32(result.User.user_id);
                            }
                        }

                        if (objBO.UserId != 0 & objBO.userGroup_Id != 0 & objBO.startDate != null & objBO.ExpiryDate != null)
                        {
                            string tempST = Convert.ToDateTime(objBO.startDate).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                            string tempET = Convert.ToDateTime(objBO.ExpiryDate).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");

                            string bodyJson = "{\"User\": {\"name\":\"" + objBO.EmpName + "\",\"email\": \"" +
                                objBO.EmailId + "\",\"user_id\": " + objBO.UserId
                                + ",\"user_group_id\": {\"id\": " + objBO.userGroup_Id + "}," +
                                "\"disabled\": \"false\",\"start_datetime\": \"" + tempST + "\"," +
                                "\"expiry_datetime\": \"" + tempET + "\"}}";
                            StringContent sc = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                            httpClient = new HttpClient();
                            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                            httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                            HttpResponseMessage response = await httpClient.PostAsync(url, sc);

                            if (response.IsSuccessStatusCode)
                            {
                                objBO.p_flag = 1;
                                UpdateProcessFlag_Employee(objBO);
                            }
                        }
                    }
                    else
                    {
                        url = url + "/" + objBO.UserId;
                        if (objBO.UserId != 0 & objBO.userGroup_Id != null & objBO.userGroup_Id != 0)
                        {
                            string bodyJson = "{\"User\": {\"name\":\"" + objBO.EmpName + "\",\"email\": \"" +
                               objBO.EmailId + "\""
                               + ",\"user_group_id\": {\"id\": " + objBO.userGroup_Id + "}}}";

                            StringContent sc = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                            httpClient = new HttpClient();
                            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                            httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                            HttpResponseMessage response = await httpClient.PutAsync(url, sc);

                            if (response.IsSuccessStatusCode)
                            {
                                objBO.p_flag = 1;
                                UpdateProcessFlag_Employee(objBO);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Update employee details error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }

        public void UpdateProcessFlag_Employee(UserDetailsBO model)
        {
            try
            {
                if (DBType == "Oracle")
                {
                    MonitoringEvent objBL = new MonitoringEvent();
                    ResponseMessageBO response = objBL.UpdateEmployeeTable(model);

                    if (!response.Status)
                    {
                        string path = LogFolderPath + "Log.txt";
                        using (StreamWriter writer = new StreamWriter(path, true))
                        {
                            writer.WriteLine(string.Format("Employee details update/create failed: " + response.ExceptionMessage + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                            writer.Close();
                        }
                    }
                }
                else if (DBType == "SQLServer")
                {
                    MonitoringEventForSQL objSQLBL = new MonitoringEventForSQL();
                    ResponseMessageBO response = objSQLBL.UpdateEmployeeTable(model);

                    if (!response.Status)
                    {
                        string path = LogFolderPath + "Log.txt";
                        using (StreamWriter writer = new StreamWriter(path, true))
                        {
                            writer.WriteLine(string.Format("Employee details update/create failed: " + response.ExceptionMessage + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                            writer.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error update employee details: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }

        }
        #endregion

        #region Functions for update access group and database.
        static void UpdateAccessGroup(List<UserDetailsBO> list_objBO)
        {
            foreach (UserDetailsBO item in list_objBO)
            {
                Service1 obj = new Service1();
                var uri = LoginUrl;
                var t = Task.Run(() => obj.SendPostRequest(uri, 1));
                t.Wait(1 * 60 * 1000);

                uri = UpdateAGUrl;
                var t2 = Task.Run(() => obj.UpdateAG_API(uri, obj.sessionIdForThread, item));
                t2.Wait(1 * 60 * 1000);
            }

        }

        public async Task UpdateAG_API(string uri, string sessionId, UserDetailsBO objBO)
        {
            string path;
            try
            {
                uri = uri + "/" + objBO.UserId;
                objBO.Comment = null;
                if (sessionId != null & sessionId != "")
                {
                    string accessGrpString = "";
                    //string[] values = objBO.AccessGrp_Id.Split(',');
                    string value = objBO.AccessRights_Id.ToString();
                    bool flag = false;

                    HttpClient httpClient = new HttpClient();
                    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                    httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    string responsebody_Log = await response.Content.ReadAsStringAsync();
                    string assigned_AGs = ",";
                    string final_assignedAG = "";
                    if (response.IsSuccessStatusCode)
                    {
                        if (responsebody_Log != null && responsebody_Log != "")
                        {
                            dynamic result = JsonConvert.DeserializeObject(responsebody_Log);

                            if (result["User"].ContainsKey("access_groups"))
                            {
                                var Count = result["User"]["access_groups"].Count;

                                int i = 0;
                                while (i < Count)
                                {
                                    assigned_AGs += Convert.ToString(result.User.access_groups[i].id) + ",";
                                    i++;
                                }
                            }
                            final_assignedAG = assigned_AGs;
                        }

                        if (objBO.Flag == 1)
                        {
                            string tempItem = "," + value + ",";
                            if (assigned_AGs.Contains(tempItem))
                                assigned_AGs = assigned_AGs.Replace("," + value, "");
                        }
                        else
                        {
                            string tempItem = "," + value + ",";
                            if (!assigned_AGs.Contains(tempItem))
                                assigned_AGs += value + ",";
                        }

                        if (final_assignedAG != assigned_AGs)
                            flag = true;

                        assigned_AGs.TrimStart(',');
                        assigned_AGs.TrimEnd(',');
                        string[] finalValues;
                        if (assigned_AGs != "" && assigned_AGs != null)
                            finalValues = assigned_AGs.Split(',');
                        else
                            finalValues = null;

                        if (flag)
                        {
                            if (finalValues.Length != 0 && finalValues != null)
                            {
                                StringBuilder sb = new StringBuilder();
                                foreach (string val in finalValues)
                                {
                                    int id;
                                    if (int.TryParse(val, out id))
                                    {
                                        sb.Append("{\"id\":");
                                        sb.Append(id);
                                        sb.Append("},");
                                    }
                                }

                                accessGrpString = sb.ToString().TrimEnd(',');
                            }
                            else
                                accessGrpString = "";

                            httpClient = new HttpClient();
                            string data = "{\"User\": {\"access_groups\": [" + accessGrpString + "]}}";
                            StringContent sc = new StringContent(data, Encoding.UTF8, "application/json");

                            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                            httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                            response = await httpClient.PutAsync(uri, sc);

                            if (objBO.Flag == 1)
                                objBO.Rvk_Procd = DateTime.Now;
                            else
                                objBO.Grnt_Procd = DateTime.Now;

                            //This code will execute when there will be any wrong access group id.
                            if (!response.IsSuccessStatusCode)
                                objBO.Comment = "Wrong access-group Id!";
                        }
                        else
                        {
                            //The below code will execute when there is already assigned groups
                            if (objBO.Flag == 1)
                                objBO.Rvk_Procd = DateTime.Now;
                            else
                                objBO.Grnt_Procd = DateTime.Now;
                        }

                        UpdateProcessFlag_AG(objBO);
                    }
                    else
                    {
                        objBO.Comment = "Invalid user Id!";

                        if (objBO.Flag == 1)
                            objBO.Rvk_Procd = DateTime.Now;
                        else
                            objBO.Grnt_Procd = DateTime.Now;

                        UpdateProcessFlag_AG(objBO);
                    }
                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Update AG API Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }

        public void UpdateProcessFlag_AG(UserDetailsBO model)
        {
            try
            {
                if (DBType == "Oracle")
                {
                    MonitoringEvent objBL = new MonitoringEvent();
                    ResponseMessageBO response = objBL.UpdateFlag_AG(model);

                    if (!response.Status)
                    {
                        string path = LogFolderPath + "Log.txt";
                        using (StreamWriter writer = new StreamWriter(path, true))
                        {
                            writer.WriteLine(string.Format("Update failed: " + response.ExceptionMessage + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                            writer.Close();
                        }
                    }
                }
                else if (DBType == "SQLServer")
                {
                    MonitoringEventForSQL objSQLBL = new MonitoringEventForSQL();
                    ResponseMessageBO response = objSQLBL.UpdateFlag_AG(model);

                    if (!response.Status)
                    {
                        string path = LogFolderPath + "Log.txt";
                        using (StreamWriter writer = new StreamWriter(path, true))
                        {
                            writer.WriteLine(string.Format("Update failed: " + response.ExceptionMessage + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                            writer.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error Update AG Pflag: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }

        }
        #endregion

        #region Functions for multiple device for missing entries.
        public void CheckStatusFileExist()
        {
            //Check if the date log file exist or not and if do then fetch the date from there.
            try
            {
                string filePath = LogFolderPath + "DeviceStatusLogs.txt";
                bool fileExists = File.Exists(filePath);

                Service1 obj = new Service1();

                var uri = LoginUrl;
                var t = Task.Run(() => obj.SendPostRequest(uri));
                t.Wait(1 * 60 * 1000);

                if (fileExists)
                {
                    //======Add the status and device id in the global dictionary.=======//
                    uri = AllAddedDeviceUrl;
                    var t2 = Task.Run(() => obj.CallingForCheckDevice(uri, obj.sessionID, 1));
                    t2.Wait(1 * 60 * 1000);

                    //=====Fetch status of device from file================//
                    var fileData = ReadFile(filePath);

                    //Compare the dictionary and file data for status change of device and
                    //accordingly fetch the log date and device id.
                    lock (lockObject)
                    {
                        string deviceDT, deviceId;
                        int status;
                        foreach (var item in deviceStatus)
                        {
                            deviceId = item.Key;
                            status = item.Value.DeviceStatus;
                            if (fileData.ContainsKey(deviceId))
                            {
                                int statusInFile = Convert.ToInt32(fileData[deviceId].DeviceStatus);
                                if (statusInFile != status)
                                {
                                    if (status == 1)
                                    {
                                        deviceDT = fileData[deviceId].LogDate;
                                        //var t1 = Task.Run(() => MyTask(deviceDT, deviceId));
                                        //t1.Wait((1/2)* 60 * 1000);
                                        new Thread(new ParameterizedThreadStart(MyTask)).Start(new object[] { deviceDT, deviceId });
                                        //Thread thread = new Thread(() => MyTask(deviceDT, deviceId));
                                        //thread.Start();
                                    }
                                    fileData[deviceId] = item.Value;
                                }
                            }
                            else
                            {
                                fileData.Add(item.Key, item.Value);
                            }
                        }

                        //=====Update the file with the latest status store in the dictionary======//
                        UpdateStatusFile(filePath, fileData);
                    }
                }
                else
                {
                    //======Add the status and device id in the global dictionary.=======//
                    uri = AllAddedDeviceUrl;
                    var t2 = Task.Run(() => obj.CallingForCheckDevice(uri, obj.sessionID, 1));
                    t2.Wait(1 * 60 * 1000);

                    filePath = LogFolderPath + "DeviceStatusLogs.txt";
                    if (deviceStatus != null)
                    {
                        UpdateStatusFile(filePath, deviceStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("CheckStatusFileExist function Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " +
                        DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }

        }
        static void MyTask(string deviceDT, string deviceId)
        {
            Service1 obj = new Service1();
            var uri = LoginUrl;
            var t = Task.Run(() => obj.SendPostRequest(uri, 1));
            t.Wait(1 * 60 * 1000);

            uri = SearchUrl;
            var t2 = Task.Run(() => obj.SearchAPIWithDeviceId(uri, obj.sessionIdForThread, deviceDT, deviceId));
            t2.Wait(1 * 60 * 1000);

        }
        static void MyTask(object argVal)
        {
            object[] paramsVal = (object[])argVal;
            MyTask(paramsVal[0].ToString(), paramsVal[1].ToString());
        }
        public void CheckDeviceEvent()
        {
            string filePath = LogFolderPath + "DeviceStatusLogs.txt";
            Service1 obj = new Service1();

            var uri = LoginUrl;
            var t = Task.Run(() => obj.SendPostRequest(uri));
            t.Wait(1 * 60 * 1000);

            //======Add the status and device id in the global dictionary.=======//
            uri = AllAddedDeviceUrl;
            var t2 = Task.Run(() => obj.CallingForCheckDevice(uri, obj.sessionID, 1));
            t2.Wait(1 * 60 * 1000);

            //=====Fetch status of device from file================//
            var fileData = ReadFile(filePath);

            lock (lockObject)
            {
                string deviceDT, deviceId;
                int status;
                foreach (var item in deviceStatus)
                {
                    deviceId = item.Key;
                    status = item.Value.DeviceStatus;
                    if (fileData.ContainsKey(deviceId))
                    {
                        int statusInFile = Convert.ToInt32(fileData[deviceId].DeviceStatus);
                        if (statusInFile != status)
                        {
                            if (status == 1)
                            {
                                deviceDT = fileData[deviceId].LogDate;
                                //var t1 = Task.Run(() => MyTask(deviceDT, deviceId));
                                //t1.Wait((1 / 2) * 60 * 1000);
                                new Thread(new ParameterizedThreadStart(MyTask)).Start(new object[] { deviceDT, deviceId });
                            }
                            fileData[deviceId] = item.Value;
                        }
                    }
                    else
                    {
                        fileData.Add(item.Key, item.Value);
                    }
                }

                //=====Update the file with the latest status store in the dictionary======//
                UpdateStatusFile(filePath, fileData);
            }

        }
        #endregion

        #region Task for calling of Login API of BioStar2 
        public async Task SendPostRequest(string uri, int flag = 0)
        {
            try
            {
                HttpClient httpClient = new HttpClient();

                Dictionary<string, string> dicLoginUser = new Dictionary<string, string>();
                dicLoginUser.Add("login_id", UserName);
                dicLoginUser.Add("password", Password);

                Dictionary<string, object> dicLogin = new Dictionary<string, object>();
                dicLogin.Add("User", dicLoginUser);

                string jsonLoginUser = JsonConvert.SerializeObject(dicLogin);

                StringContent sc = new StringContent(jsonLoginUser, Encoding.UTF8, "application/json");

                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                HttpResponseMessage response = await httpClient.PostAsync(uri, sc);
                string responsebody = await response.Content.ReadAsStringAsync();

                bool isSessionIDContained = response.Headers.Contains("bs-session-id");
                if (isSessionIDContained == true)
                {

                    IEnumerable<string> sessionEnum = response.Headers.GetValues("bs-session-id");
                    foreach (string element in sessionEnum)
                    {
                        //this session is for only thread calling.
                        if (flag == 1)
                            sessionIdForThread = element;

                        else
                            sessionID = element;
                    }
                }

                //string path = LogFolderPath + "Log.txt";
                //using (StreamWriter writer = new StreamWriter(path, true))
                //{
                //    writer.WriteLine("1st API: #_Count: " + count + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                //    writer.Close();
                //}
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Login Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }
        #endregion

        #region Task for calling the search API of BioStar2 

        public async Task SendPostRequestForOldEvent(string uri, string sessionId, string SD, string ED, string currentDate)
        {
            string path = LogFolderPath + "Log.txt";
            try
            {
                if (sessionId != null & sessionId != "")
                {
                    int dateTimeInterval = Convert.ToInt32(ConfigurationManager.AppSettings["DateTimeInterval"]);
                    DateTime tempCD = (DateTime.ParseExact(currentDate, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
                    DateTime tempSD = (DateTime.ParseExact(SD, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));

                    while (tempSD < tempCD)
                    {
                        HttpClient httpClient = new HttpClient();
                        string queryDdata = "{\"Query\": {\"limit\": " + QueryLimit + ",\"conditions\": [{\"column\": \"datetime\",\"operator\": 3,\"values\": [\"" + SD + "\",\"" + ED + "\"]}]," +
            "\"orders\": [{\"column\": \"datetime\",\"descending\": true}]}}";
                        StringContent sc = new StringContent(queryDdata, Encoding.UTF8, "application/json");

                        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                        httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                        HttpResponseMessage response = await httpClient.PostAsync(uri, sc);
                        string responsebody_Log = await response.Content.ReadAsStringAsync();

                        if (responsebody_Log != null && responsebody_Log != "")
                        {
                            var data = responsebody_Log;

                            int dataLength = data.Length;
                            var flag = true;
                            if (dataLength <= 137)
                            {
                                flag = false;
                            }

                            if (flag)
                            {
                                MonitoringEventBO eventBO = new MonitoringEventBO();
                                eventBO.JsonDataString = data;

                                InsertEvent(eventBO);
                            }
                        }

                        TimeSpan ts = tempCD - tempSD;
                        if (ts.TotalSeconds >= dateTimeInterval)
                        {
                            SD = ED;
                            DateTime date = (DateTime.ParseExact(SD, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddSeconds(dateTimeInterval);
                            ED = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");

                            tempSD = date;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Old event search_Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }

                throw;
            }
        }

        public async Task SendPostRequestForEvent(string uri, string sessionId)
        {
            string path = LogFolderPath + "Log.txt";
            try
            {
                if (sessionId != null & sessionId != "")
                {
                    // count++; //Count is used for preventing to reset time to current, at every elapsed.

                    int dateTimeInterval = Convert.ToInt32(ConfigurationManager.AppSettings["DateTimeInterval"]);

                    if (count == 1)
                    {
                        count++;
                        if (startDate == "" || startDate == null)
                        {
                            startDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                            endDate = DateTime.UtcNow.AddSeconds(dateTimeInterval).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                        }
                        else
                        {
                            DateTime date = (DateTime.ParseExact(startDate, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddSeconds(dateTimeInterval);
                            endDate = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                        }
                    }
                    else
                    {
                        var CurrentDate = DateTime.UtcNow;
                        var SD = (DateTime.ParseExact(startDate, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
                        TimeSpan ts = CurrentDate - SD;

                        if (ts.TotalSeconds >= dateTimeInterval)
                        {
                            startDate = endDate;
                            DateTime date = (DateTime.ParseExact(startDate, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddSeconds(dateTimeInterval);
                            endDate = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                        }

                    }

                    //path = LogFolderPath + "Log.txt";
                    //using (StreamWriter writer = new StreamWriter(path, true))
                    //{
                    //    writer.WriteLine("2nd API Events: SD: " + startDate + " _#_ED: " + endDate + "_#_Count: " + count + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                    //    writer.Close();
                    //}

                    //startDate = "2023-05-12T16:07:10.0382264Z";

                    HttpClient httpClient = new HttpClient();
                    string data = "{\"Query\": {\"limit\": " + QueryLimit + ",\"conditions\": [{\"column\": \"datetime\",\"operator\": 3,\"values\": [\"" + startDate + "\",\"" + endDate + "\"]}]," +
        "\"orders\": [{\"column\": \"datetime\",\"descending\": true}]}}";
                    StringContent sc = new StringContent(data, Encoding.UTF8, "application/json");

                    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                    httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                    HttpResponseMessage response = await httpClient.PostAsync(uri, sc);
                    responsebody_Log = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Search_Error: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }

                throw;
            }
        }

        /// <summary>
        /// The below function is for reading the date log file and return the 
        /// last entered dates.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public string ReadLastDateFromFile(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream))
            {
                string currentLine;
                string lastLine = null;

                while ((currentLine = streamReader.ReadLine()) != null)
                {
                    lastLine = currentLine;
                }

                return lastLine;
            }
        }

        /// <summary>
        /// The below function is for fetching the monitoring log of a perticular device.
        /// </summary>
        /// <returns></returns>
        public async Task SearchAPIWithDeviceId(string uri, string sessionId, string deviceST, string deviceId)
        {
            string path = LogFolderPath + "Log.txt";
            try
            {
                if (sessionId != null & sessionId != "")
                {

                    int dateTimeInterval = Convert.ToInt32(ConfigurationManager.AppSettings["DateTimeInterval"]);
                    //===Subtract 1 minutes from the start date, to ensure no record get missed.====//
                    deviceST = (DateTime.ParseExact(deviceST, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddMinutes(-3).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                    string ED = (DateTime.ParseExact(deviceST, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddSeconds(dateTimeInterval).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                    var SD = (DateTime.ParseExact(deviceST, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
                    var CurrentDate = DateTime.UtcNow;

                    while (SD < CurrentDate)
                    {
                        TimeSpan ts = CurrentDate - SD;
                        //====Fetch the monitoring logs=============//
                        HttpClient httpClient = new HttpClient();
                        string data = "{\"Query\": {\"limit\": " + QueryLimit +
                            ",\"conditions\": [{\"column\": \"datetime\",\"operator\": 3,\"values\": [\"" +
                            deviceST + "\",\"" + ED + "\"]},{\"column\": \"device_id\",\"operator\": 0,\"values\": [\"" +
                            deviceId + "\"]}]," + "\"orders\": [{\"column\": \"datetime\",\"descending\": true}]}}";
                        StringContent sc = new StringContent(data, Encoding.UTF8, "application/json");

                        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                        httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                        var response = await httpClient.PostAsync(uri, sc);
                        var responsebody_Log = await response.Content.ReadAsStringAsync();

                        //===========End of API call===============//

                        //====Call the insert function for the insertion of the monitoring logs.==//

                        int dataLength = responsebody_Log.Length;
                        var flag = true;
                        if (dataLength <= 137)
                        {
                            flag = false;
                        }

                        if (flag)
                        {
                            MonitoringEventBO eventBO = new MonitoringEventBO();
                            eventBO.JsonDataString = responsebody_Log;

                            InsertEvent(eventBO);
                        }
                        //===========End insert calling===============//

                        //======For increment of the date-time=========//
                        if (ts.TotalSeconds >= dateTimeInterval)
                        {
                            deviceST = ED;
                            DateTime date = (DateTime.ParseExact(deviceST, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)).AddSeconds(dateTimeInterval);
                            ED = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");
                            SD = (DateTime.ParseExact(deviceST, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
                        }
                        //=========End==============//

                    }

                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Device Id Search_Error: " + ex + "_#_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }

                throw;
            }
        }
        #endregion

        #region List all added device API of BioStar2 
        /// <summary>
        /// Task for checking if their is any device or the devices are connected.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task CallingForCheckDevice(string uri, string sessionId, int flag = 0)
        {
            try
            {
                if (sessionId != null & sessionId != "")
                {
                    HttpClient httpClient = new HttpClient();

                    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                    httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    string responsebody = await response.Content.ReadAsStringAsync();

                    var json = new JavaScriptSerializer();
                    dynamic data = JsonConvert.DeserializeObject(responsebody);
                    var TotalDevice = Convert.ToInt32(data.DeviceCollection.total);
                    int i = 0;

                    if (TotalDevice > 0)
                    {
                        while (i < TotalDevice)
                        {
                            var status = data.DeviceCollection.rows[i].status;
                            if (Convert.ToInt32(status) == 1)
                            {
                                FlagIfDeviceExist = true;
                                break;
                            }
                            i++;
                        }
                        //===Add the status and device id in the global dictionary for the.==========//

                        string serialNo;

                        if (flag == 1)
                        {
                            i = 0;
                            while (i < TotalDevice)
                            {
                                DeviceLogBO obj = new DeviceLogBO();
                                serialNo = data.DeviceCollection.rows[i].id;
                                obj.DeviceStatus = Convert.ToInt32(data.DeviceCollection.rows[i].status);
                                obj.LogDate = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'");

                                if (deviceStatus != null)
                                {
                                    if (deviceStatus.ContainsKey(serialNo))
                                    {
                                        var statusDic = GetValueFromDictionary(serialNo);
                                        if (obj.DeviceStatus != statusDic.DeviceStatus)
                                        {
                                            UpdateToDictionary(serialNo, obj);
                                        }
                                    }
                                    else
                                        AddToDictionary(serialNo, obj);
                                }
                                else
                                    AddToDictionary(serialNo, obj);

                                i++;
                            }
                        }
                        //===========End====================//
                    }
                    else
                        FlagIfDeviceExist = false;
                }

                //string path = LogFolderPath + "Log.txt";
                //using (StreamWriter writer = new StreamWriter(path, true))
                //{
                //    writer.WriteLine("3rd API: ");
                //    foreach (var item in deviceStatus)
                //    {
                //        writer.WriteLine("Key: " + item.Key + " Values: " +
                //            item.Value.DeviceStatus + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"));
                //    }
                //    writer.Close();
                //}
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Device Connection Error: " + ex + "_##_" + ex.Message +
                        " flag: " + FlagIfDeviceExist + "_#_" + ex.StackTrace + " " +
                        DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }

        #endregion

        #region Function for calling API's
        public void Working()
        {
            var uri = LoginUrl;
            var t = Task.Run(() => SendPostRequest(uri));
            t.Wait(1 * 60 * 1000);

            uri = AllAddedDeviceUrl;
            var t2 = Task.Run(() => CallingForCheckDevice(uri, sessionID));
            t2.Wait(1 * 60 * 1000);

            if (FlagIfDeviceExist)
            {
                uri = SearchUrl;
                var t1 = Task.Run(() => SendPostRequestForEvent(uri, sessionID));
                t1.Wait(1 * 60 * 1000);
            }
            //var data = "[{\"EventCode\":\"4103\",\"EventName\":\"VERIFY_SUCCESS_CARD_PIN\"}]";

            if (responsebody_Log != null && responsebody_Log != "")
            {
                var data = responsebody_Log;

                int dataLength = data.Length;
                var flag = true;
                if (dataLength <= 137)
                {
                    flag = false;
                }

                if (flag)
                {
                    MonitoringEventBO eventBO = new MonitoringEventBO();
                    eventBO.JsonDataString = data;

                    InsertEvent(eventBO);
                }
            }
        }
        #endregion

        #region Calling of Insert function
        /// <summary>
        /// Description: Calling BL for inserting the monitor log.
        /// </summary>
        /// <param name="eventBO"></param>
        //[Obsolete]
        public void InsertEvent(MonitoringEventBO eventBO)
        {
            try
            {
                ResponseMessageBO response = new ResponseMessageBO();
                if (DBType == "Oracle")
                {
                    MonitoringEvent objBL = new MonitoringEvent();
                    response = objBL.InsertMonitoringLog(eventBO, dictionaryEventDesc);
                }
                else
                {
                    MonitoringEventForSQL objSqlBL = new MonitoringEventForSQL();
                    response = objSqlBL.InsertMonitoringLog(eventBO, dictionaryEventDesc);
                }
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error Insert: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }
        #endregion

        #region Function for update and read value of dictionary.
        static void AddToDictionary(string key, DeviceLogBO value)
        {
            lock (lockObject)
            {
                deviceStatus.Add(key, value);
            }
        }
        static void UpdateToDictionary(string key, DeviceLogBO value)
        {
            lock (lockObject)
            {
                deviceStatus[key] = value;
            }
        }

        static DeviceLogBO GetValueFromDictionary(string key)
        {
            lock (lockObject)
            {
                DeviceLogBO value = new DeviceLogBO();
                deviceStatus.TryGetValue(key, out value);
                return value;
            }
        }
        #endregion

        #region Functions for reead and update device status text file.
        static void UpdateStatusFile(string filePath, Dictionary<string, DeviceLogBO> dictionary)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(dictionary);
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error while updating status file: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }
        static Dictionary<string, DeviceLogBO> ReadFile(string filePath)
        {
            Dictionary<string, DeviceLogBO> resultObj = new Dictionary<string, DeviceLogBO>();
            try
            {
                string jsonString = File.ReadAllText(filePath);

                // Deserialize the JSON string into a dictionary
                resultObj = JsonConvert.DeserializeObject<Dictionary<string, DeviceLogBO>>(jsonString);

            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error while reading status file: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }

            return resultObj;
        }
        #endregion

        #region Function for calling Event description
        static void SetEventDescription()
        {
            Service1 obj = new Service1();
            var uri = LoginUrl;
            var t = Task.Run(() => obj.SendPostRequest(uri, 1));
            t.Wait(1 * 60 * 1000);

            uri = ViewTypeUrl;
            var t2 = Task.Run(() => obj.ViewTypeAPI(uri, obj.sessionIdForThread));
            t2.Wait(1 * 60 * 1000);
        }

        public async Task ViewTypeAPI(string uri, string sessionId)
        {
            string path = LogFolderPath + "Log.txt";
            try
            {
                if (sessionId != null & sessionId != "")
                {
                    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                    HttpClient httpClient = new HttpClient();

                    httpClient.DefaultRequestHeaders.Add("bs-session-id", sessionId);
                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    string responsebody_Log = await response.Content.ReadAsStringAsync();

                    var json = new JavaScriptSerializer();
                    dynamic data = JsonConvert.DeserializeObject(responsebody_Log);
                    int Count = Convert.ToInt32(data.EventTypeCollection.total == 0 || data.EventTypeCollection.total == null ? 0 : data.EventTypeCollection.total);

                    int i = 0;

                    while (i < Count)
                    {
                        dictionaryEventDesc.Add(Convert.ToString(data.EventTypeCollection.rows[i].code), Convert.ToString(data.EventTypeCollection.rows[i].name));
                        i++;
                    }
                }
            }
            catch (Exception ex)
            {
                path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Event type description: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }

                throw;
            }
        }
        #endregion

        #region Stop event of service
        protected override void OnStop()
        {
            try
            {
                this.timer.Stop();
                this.timer.Dispose();
            }
            catch (Exception ex)
            {
                string path = LogFolderPath + "Log.txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(string.Format("Error Stop: " + ex + "_##_" + ex.Message + "_#_" + ex.StackTrace + " " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff")));
                    writer.Close();
                }
                throw;
            }
        }
        #endregion
    }
}