using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Xml.Serialization;
using TDC.Tools.ProjectTimer.Enums;

namespace TDC.Tools.ProjectTimer.ViewModels
{
    public class EventLogEntryDto
    {
        public long InstanceId { get; set; }

        [XmlIgnore]
        public UInt16 EventId => (UInt16)InstanceId;

        [XmlIgnore]
        public EnumEventID EventAsEnum
        {
            get
            {
                if (Enum.IsDefined(typeof(EnumEventID), (int)EventId))
                {
                    return (EnumEventID)(int)EventId;
                }
                return EnumEventID.Unknown;
            }
        }

        public DateTime TimeCreated { get; set; }

        public EventLogEntryType EntryType { get; set; }

        public int Index { get; set; }

        public string UserName { get; set; }

        public string Source { get; set; }

        public string MachineName { get; set; }

        public byte[] Data { get; set; }

        public string Message { get; set; }

        public static EventLogEntryDto CreateInstance(EventLogEntry logEntry)
        {
            return new EventLogEntryDto
            {
                Data = logEntry.Data,
                EntryType = logEntry.EntryType,
                Index = logEntry.Index,
                InstanceId = logEntry.InstanceId,
                MachineName = logEntry.MachineName,
                Message = logEntry.Message,
                Source = logEntry.Source,
                TimeCreated = logEntry.TimeGenerated,
                UserName = logEntry.UserName
            };
        }

        public static EventLogEntryDto CreateInstance(EventRecord record)
        {
            return new EventLogEntryDto
            {
                Data = new byte[] { 0 },
                EntryType = record.Level.HasValue ? (EventLogEntryType) record.Level.Value:EventLogEntryType.Information,
                Index = record.RecordId.HasValue ? (int)record.RecordId.Value : 0,
                InstanceId = (long)record.Id + 2147483648,
                MachineName = record.MachineName,
                Message = record.FormatDescription(),
                Source = record.ProviderName,
                TimeCreated = record.TimeCreated ?? DateTime.MinValue,
                UserName = record.UserId?.Value
            };
        }
    }
}