using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: log4net.Config.XmlConfigurator(ConfigFile="DrComLoginViaHttp.exe.config", Watch = true)]
namespace DrComLoginViaHttp
{
    public partial class DrComLogin : ServiceBase
    {

        [DllImport("wininet.dll", EntryPoint = "InternetGetConnectedState")]
        public extern static bool InternetGetConnectedState(out flagsInternetConnectionStatus connectionDescription, int reservedValue);

        [Flags]
        public enum flagsInternetConnectionStatus
        {
            INTERNET_CONNECTION_CONFIGURED = 0x40,
            INTERNET_CONNECTION_LAN = 0x02,
            INTERNET_CONNECTION_MODEM = 0x01,
            INTERNET_CONNECTION_MODEM_BUSY = 0x08,
            INTERNET_CONNECTION_OFFLINE = 0x20,
            INTERNET_CONNECTION_PROXY = 0x04,
            INTERNET_RAS_INSTALLED = 0x10
        }

        public DrComLogin()
        {
            InitializeComponent();
            
        }

        static DrComLogin()
        {
            if (!EventLog.SourceExists("DrComLoginViaHttp"))
                EventLog.CreateEventSource("DrComLoginViaHttp", "Application");
            eventLog.Source = "DrComLoginViaHttp";
        }

        System.Threading.Thread thr;
        Boolean isgood = false;
        static EventLog eventLog = new EventLog("Application");

        static ILog debuglog = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        static void log(string logcontent)
        {

            eventLog.WriteEntry(logcontent);
        }

        public enum TypeOfLogin
        {
            Type_Plain,
            Type_Md5
        }

        private static bool funcSubmitToServer(byte[] input,StringBuilder strToReturn)
        {           
            try
            {
                debuglog.Debug("LoginDrDotComService:Enter Func LoginDrDotComService");

                var post = System.Net.HttpWebRequest.Create("http://10.1.1.254");

                debuglog.Debug("LoginDrDotComService:Create HttpWebRequest");

                post.Method = "POST";

                //解决开机启动很慢的问题，查阅资料知，默认会试图使用proxy登录，因此导致需要超时后才能取到requeststream。
                post.Proxy = null;

                byte[] toout = input;
                post.ContentLength = toout.Length;
                debuglog.Debug("LoginDrDotComService:Begin GetRequestStream");

                System.IO.Stream requeststream = post.GetRequestStream();
                debuglog.Debug("LoginDrDotComService:End GetRequestStream");
                debuglog.Debug("LoginDrDotComService:Begin Write");
                requeststream.Write(toout, 0, toout.Length);
                debuglog.Debug("LoginDrDotComService:End Write");
                debuglog.Debug("LoginDrDotComService:GetResponse");
                var responsetest = post.GetResponse();
                debuglog.Debug("LoginDrDotComService:GetResponse END");
                var responststream = responsetest.GetResponseStream();
                debuglog.Debug("LoginDrDotComService:GetResponseStream END");
                strToReturn.Append(new System.IO.StreamReader(responststream, System.Text.Encoding.GetEncoding("GB2312")).ReadToEnd());
                requeststream.Close();
                if (strToReturn.ToString().Contains("您已经成功登录") || strToReturn.ToString().Contains("已使用时间"))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (System.IO.IOException e)
            {
                return false;
            }
            
        }

        private static volatile int mLoginDrComService = 1;
        public static void LoginDrDotComService(string username, string password,TypeOfLogin typeOfLogin, StringBuilder orginstringBuilder)
        {
            if (mLoginDrComService == 1)
            {
                ++mLoginDrComService;
                if (mLoginDrComService == 2)
                {
                    StringBuilder outputOrginString = new StringBuilder();
                    bool retrunsresult;
                    if (typeOfLogin == TypeOfLogin.Type_Plain)
                    {
                        retrunsresult = funcSubmitToServer(System.Text.Encoding.ASCII.GetBytes("DDDDD=" + DrComLoginViaHttp.Properties.Settings.Default.USERNAME + "&upass=" + DrComLoginViaHttp.Properties.Settings.Default.PASSWORD + "&0MKKey=%B5%C7%C2%BC+Login&v6ip="), orginstringBuilder);
                    }
                    else
                    {
                        retrunsresult = funcSubmitToServer(System.Text.Encoding.ASCII.GetBytes(ccg.ToAscii(ccg.GetStr(username, password))), orginstringBuilder);
                    }

                    if (retrunsresult == true)
                    {
                        debuglog.Info("Login Success");
                        log("成功登录");
                    }
                    else if (outputOrginString.ToString().Contains("账号或密码不对，请重新输入"))
                    {
                        debuglog.Error("PASSWORD FAIL");

                        log("密码错误");
                    }
                    else if (outputOrginString.ToString().Contains("正在使用"))
                    {
                        debuglog.Error("CURRECT USING FAIL");
                        log("该账号正在使用，如下是原文：\n" + outputOrginString);
                    }
                    else
                    {
                        debuglog.Error("ELSE FAIL : " + outputOrginString);

                        log("其他错误，以下是错误原文：\n" + outputOrginString);
                    }
                }
                --mLoginDrComService;
            }
        }
        public static volatile int runningFlag = 0;
        protected override void OnStart(string[] args)
        {
            
            debuglog.Debug("OnStart");
            flagsInternetConnectionStatus ffout;
            if (InternetGetConnectedState(out ffout, 0))
            {
                debuglog.Debug("connections good, sends login");
                LoginDrDotComService(DrComLoginViaHttp.Properties.Settings.Default.USERNAME,
                                            DrComLoginViaHttp.Properties.Settings.Default.PASSWORD,
                                            TypeOfLogin.Type_Md5,
                                            new StringBuilder());
            }
            else
            {
                debuglog.Debug("connections bad, not login");
            }

            thr = new System.Threading.Thread(
                () =>
                {
                    StringBuilder sb = new StringBuilder();
                    int pingsecond = 1000;
                    while (true)
                    {
                        if (runningFlag != 0)
                        {
                            runningFlag = 100;
                            break;
                        }
                        flagsInternetConnectionStatus flagout;
                        if (!InternetGetConnectedState(out flagout, 0))
                        {
                            debuglog.Debug("UNCONNECTED - WAIT FOR 1000 ms");
                            System.Threading.Thread.Sleep(1000);
                            continue;
                        }
                        debuglog.Debug("now ping baidu");
                        System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
                        System.Net.NetworkInformation.PingReply pingreply = null;
                        try
                        {
                            pingreply = ping.Send("www.baidu.com");
                        }
                        catch (System.Net.NetworkInformation.PingException e)
                        {
                            pingsecond = 1000;
                            continue;
                        }
                        if (pingreply.Status != System.Net.NetworkInformation.IPStatus.Success)
                        {
                            debuglog.Debug("login needs~");
                            if (mLoginDrComService == 1)
                            {
                                LoginDrDotComService(DrComLoginViaHttp.Properties.Settings.Default.USERNAME,
                                            DrComLoginViaHttp.Properties.Settings.Default.PASSWORD,
                                            TypeOfLogin.Type_Md5,
                                            sb);
                                pingsecond = 1000;
                                debuglog.Debug("reset pingsecond to 1000 ms");
                            }
                            
                        }
                        else
                        {
                            if (pingsecond <= 30000)
                            {
                                pingsecond += 1000;
                                debuglog.Debug("add ping second by 1000 ms");
                            }
                        }
                        debuglog.Debug("pings good");
                        System.Threading.Thread.Sleep(pingsecond);
                    }

                }
                );
            

            debuglog.Debug("START PING THREAD");
            runningFlag = 0;
            thr.Start();
        }

        protected override void OnStop()
        {
            debuglog.Info("OnSTOP");
            thr.Abort();
        }

        protected override void OnContinue()
        {
            debuglog.Info("OnContinue");
            thr.Abort();
            OnStart(new string[] { });
            
        }
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            debuglog.Info("OnPowerEvent:" + powerStatus.ToString());
            switch (powerStatus)
            {
                case PowerBroadcastStatus.ResumeAutomatic:
                case PowerBroadcastStatus.ResumeCritical:
                case PowerBroadcastStatus.ResumeSuspend:
                    debuglog.Info("RELOGIN ");
                    runningFlag = 1;
                    thr.Join();
                    OnStart(new string[] { });
                    return true;
                    

                
                    
            }
            return base.OnPowerEvent(powerStatus);
            
        }
    }
}
