using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;

class Program
{
    class Config
    {
        public string MailServer { get; set; } = "smtp.example.com";
        public int Port { get; set; } = 587;
        public string FromAdress { get; set; } = "youremail@example.com";
        public string Password { get; set; } = "yourpassword";
        public string Recipient { get; set; } = "john.doe@example.com";
        public int WarningThreshold { get; set; } = 10;
    }

    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            switch (args[0].ToLower())
            {
                case "create_autostart_helper_files":
                    CreateAutostartRegistry();
                    return;
                case "create_task_scheduler_entries":
                    CreateTaskSchedulerEntry();
                    return;
            }
        }

        Config config = ReadConfig() ?? CreateDefaultConfig();
        string mailContent = GenerateDiskReport(config);
        Console.WriteLine("\n--- Mail Content ---\n" + mailContent);
        SendMailAsync(config, "Disk Space Report", mailContent);
    }

    static string GenerateDiskReport(Config config)
    {
        string report = $"Hostname: {Environment.MachineName}\n";
        long gbDivisor = 1024 * 1024 * 1024;

        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            foreach (var drive in drives)
            {
                try
                {
                    long freeSpaceGB = drive.AvailableFreeSpace / gbDivisor;
                    string warning = freeSpaceGB <= config.WarningThreshold ? "WARNING! " : "";
                    report += $"DISK {drive.Name} has {freeSpaceGB} GB left free space. {warning}\n";

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to fetch details for drive {drive.Name}: {ex.Message}");
                }
            }
            return report;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    static async Task SendMailAsync(Config config, string subject, string body)
    {
        try
        {
            MailMessage msg = new MailMessage(config.FromAdress, config.Recipient, subject, body);
            SmtpClient client = new SmtpClient(config.MailServer, config.Port)
            {
                Credentials = new NetworkCredential(config.FromAdress, config.Password),
                EnableSsl = true
            };
            await client.SendMailAsync(msg);
            Console.WriteLine("Email sent successfully to " + config.Recipient);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to send email: " + ex.Message);
        }
    }
    
    static Config ReadConfig()
    {
        if (File.Exists("config.json"))
        {
            string json = File.ReadAllText("config.json");
            return JsonSerializer.Deserialize<Config>(json);
        }
        return null;
    }

    static Config CreateDefaultConfig()
    {
        Config defaultConfig = new Config();
        File.WriteAllText("config.json", JsonSerializer.Serialize(defaultConfig));
        Console.WriteLine("Default config created at config.json");
        return defaultConfig;
    }

    static void CreateAutostartRegistry()
    {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        string regFileContent = $"[HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run]\n\"HDDWarn\"=\"{exePath}\"";
        File.WriteAllText("autostart.reg", regFileContent);
        Console.WriteLine("Auto-start registry file created at autostart.reg");
    }

    static void CreateTaskSchedulerEntry()
    {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        string xmlContent = 
            $@"<?xml version='1.0' encoding='UTF-16'?>
            <Task version='1.3' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>
              <Triggers>
                <CalendarTrigger>
                  <Repetition><Interval>PT24H</Interval></Repetition>
                  <StartBoundary>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</StartBoundary>
                </CalendarTrigger>
              </Triggers>
              <Actions>
                <Exec><Command>{exePath}</Command></Exec>
              </Actions>
            </Task>";
        File.WriteAllText("task_scheduler.xml", xmlContent);
        Process.Start("schtasks", $"/create /tn 'HDDWarn Task' /xml task_scheduler.xml /f");
        Console.WriteLine("Task scheduler entry successfully created.");
    }
}
