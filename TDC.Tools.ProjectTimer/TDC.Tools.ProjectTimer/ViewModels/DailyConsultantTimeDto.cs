using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

namespace TDC.Tools.ProjectTimer.ViewModels
{
    public class DailyConsultantTimeDto
    {
        public Guid Id { get; set; }

        public List<int> EventLogDtoIds { get; set; }

        public DateTime? ConsultantStart { get; set; }

        public DateTime? ConsultantEnd { get; set; }

        public string Description { get; set; }

        [XmlIgnore]
        public TimeSpan ConsultantBreakTime { get; set; }

        [Browsable(false)]
        [XmlElement(DataType = "duration", ElementName = "ConsultantBreakTime")]
        public string ConsultantBreakTimeString
        {
            get => XmlConvert.ToString(ConsultantBreakTime);
            set => ConsultantBreakTime = string.IsNullOrEmpty(value) ?
                TimeSpan.Zero : XmlConvert.ToTimeSpan(value);
        }
    }
}