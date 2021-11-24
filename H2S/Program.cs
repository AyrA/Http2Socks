using System;
using System.IO;
#if !DEBUG
using System.ServiceProcess;
#else
using System.Reflection;
#endif

namespace H2S
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] Args)
        {
            if (!File.Exists(Tools.ConfigFile))
            {
                Tools.Log(nameof(Program), "Config does not exist. Creating defaults");
                var C = new Settings().Save();
                C.FileName = Tools.ConfigFile;
                C.Write();
            }
            else
            {
                Tools.Log(nameof(Program), "Using existing configuration");
            }

#if DEBUG
            //In debug mode, launch the service manually
            using (var Service = new Http2Socks())
            {
                Console.WriteLine("Starting service as console application...");
                var StartFunc = Service.GetType().GetMethod("OnStart", BindingFlags.NonPublic | BindingFlags.Instance);
                var StopFunc = Service.GetType().GetMethod("OnStop", BindingFlags.NonPublic | BindingFlags.Instance);
                StartFunc.Invoke(Service, new object[] { Args });
                Console.WriteLine("Service started. Press [ESC] to exit");
                while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;
                Console.WriteLine("Stopping service...");
                StopFunc.Invoke(Service, null);
                Console.WriteLine("Service stopped. Press any key to exit");
                Console.ReadKey(true);
            }
#else
            //In release mode, use the intended service control mechanism
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Http2Socks()
            };
            ServiceBase.Run(ServicesToRun);
#endif
            return 0;
        }
    }
}
