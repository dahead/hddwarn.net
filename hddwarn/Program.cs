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
        public string MailServer { get; set; }
        public int Port { get; set; }
        public string SendMail { get; set; }
        public string Password { get; set; }
        public string Recipient { get; set; }
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
        string mailContent = GenerateDiskReport();
        Console.WriteLine("\n--- Mail Content ---\n" + mailContent);
        SendMail(config, "Disk Space Report", mailContent);
    }

    static string GenerateDiskReport()
    {
        string computerName = Environment.MachineName;
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
        string report = $"Servername: {computerName}\n";
        
        foreach (var drive in drives)
        {
            long freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
            string warning = freeSpaceGB <= 10 ? "WARNING! " : "";
            report += $"{warning}DISK {drive.Name} has {freeSpaceGB} GB left free space.\n";
        }
        return report;
    }

    static void SendMail(Config config, string subject, string body)
    {
        try
        {
            MailMessage mail = new MailMessage(config.SendMail, config.Recipient, subject, body);
            SmtpClient client = new SmtpClient(config.MailServer, config.Port)
            {
                Credentials = new NetworkCredential(config.SendMail, config.Password),
                EnableSsl = true
            };
            client.Send(mail);
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
        Config defaultConfig = new Config
        {
            MailServer = "smtp.example.com",
            Port = 587,
            SendMail = "youremail@example.com",
            Password = "yourpassword",
            Recipient = "john.doe@example.com"
        };
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
        string xmlContent = $@"<?xml version='1.0' encoding='UTF-16'?>
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
