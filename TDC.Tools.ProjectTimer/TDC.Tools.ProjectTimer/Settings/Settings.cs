using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml.Serialization;

namespace TDC.Tools.ProjectTimer.Settings
{
    public class Settings
    {
        [XmlIgnore]
        private static String SettingsFile { get; } = "ProjectTimer.settings";

        public String EventLogFile { get; set; } = "EventLogEntries.xml";

        public String ConsultantTimesFile { get; set; } = "ConsultantTimes.xml";

        public bool ShowEmptyDays { get; set; } = true;

        public bool ShowTimeWarnings { get; set; } = true;

        public TimeSpan DefaultConsultantStartTime { get; set; } = new TimeSpan(9, 15, 0);

        public TimeSpan DefaultConsultantEndTime { get; set; } = new TimeSpan(18, 45, 0);

        public TimeSpan DefaultConsultantBreakTime { get; set; } = new TimeSpan(0, 45, 0);

        public bool Load()
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsFile);
            if (File.Exists(settingsPath))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(Settings));
                    using (StreamReader sr = new StreamReader(settingsPath))
                    {
                        var settings = serializer.Deserialize(sr) as Settings;
                        if (settings != null)
                        {
                            EventLogFile = settings.EventLogFile;
                            ConsultantTimesFile = settings.ConsultantTimesFile;
                            DefaultConsultantStartTime = settings.DefaultConsultantStartTime;
                            DefaultConsultantEndTime = settings.DefaultConsultantEndTime;
                            DefaultConsultantBreakTime = settings.DefaultConsultantBreakTime;
                            ShowEmptyDays = settings.ShowEmptyDays;
                            ShowTimeWarnings = settings.ShowTimeWarnings;

                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            return false;
        }

        public bool Save()
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsFile);

            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using (StreamWriter sw = new StreamWriter(settingsPath))
                {
                    serializer.Serialize(sw, this);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return false;
        }
    }
}