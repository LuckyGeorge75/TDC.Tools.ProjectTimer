using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Documents;
using Caliburn.Micro;
using TDC.Tools.ProjectTimer.Enums;
using TDC.Tools.ProjectTimer.Tools;

namespace TDC.Tools.ProjectTimer.ViewModels
{
    public class DailyTimeViewModel : PropertyChangedBase, IDataErrorInfo
    {
        private Guid _consultantTimeDtoId;
        private DateTime? _day;
        private DateTime? _start;
        private DateTime? _end;
        private TimeSpan? _totalUpTime;
        private TimeSpan? _consultantBreakTime;
        private DateTime? _consultantStart;
        private DateTime? _consultantEnd;
        private bool _overDay;
        private bool _raiseCalculationHasChangedDisabled;
        private bool _isEmptyDate;
        private string _description;
        public ObservableRangeCollection<EventLogEntryDto> EventLogEntries { get;  } = new ObservableRangeCollection<EventLogEntryDto>();

        public event EventHandler CalculationsHasChanged;

        public DailyTimeViewModel(bool isEmptyDate = false)
        {
            _isEmptyDate = isEmptyDate;
            if (_isEmptyDate)
                _consultantBreakTime = null;
        }

        public bool Add(EventLogEntryDto entry)
        {
            if (!EventLogEntries.Contains(entry))
            {
                EventLogEntries.Add(entry);
                if (!_day.HasValue)
                {
                    _day = entry.TimeCreated.Date;
                }
                UpdateTimes();
                return true;
            }
            return false;
        }

        public void SetConsultandTimes(DailyConsultantTimeDto source)
        {
            if (source != null)
            {
                foreach (var eventId in source.EventLogDtoIds)
                {
                    if (!EventLogEntries.Any(e => e.Index == eventId)) return;
                }

                _raiseCalculationHasChangedDisabled = true;

                _consultantTimeDtoId = source.Id;

                if (!IsReadOnlyConsultantEnd)
                    ConsultantEnd = source.ConsultantEnd;

                if (!IsReadOnlyConsultantStart)
                    ConsultantStart = source.ConsultantStart;

                ConsultantBreakTime = source.ConsultantBreakTime;

                Description = source.Description;

                _raiseCalculationHasChangedDisabled = false;
            }
        }

        public DailyConsultantTimeDto GetConsultantTimes()
        {
            if (EventLogEntries.Count == 0) return null;

            var id = _consultantTimeDtoId.Equals(Guid.Empty) ? Guid.NewGuid() : _consultantTimeDtoId;
            _consultantTimeDtoId = id;

            return new DailyConsultantTimeDto
            {
                Id = id,
                ConsultantBreakTime = ConsultantBreakTime ?? new TimeSpan(),
                ConsultantStart = ConsultantStart,
                ConsultantEnd = ConsultantEnd,
                EventLogDtoIds = EventLogEntries.Select(e => e.Index).ToList(),
                Description = Description
                
            };
        }

        private void UpdateTimes()
        {
            if (EventLogEntries.Count == 0) return;

            DateTime? firstStartTime = null;
            DateTime? lastEndTime = null;
            DateTime? startTime = null;
            //TimeSpan? upTime = null;

            foreach (var eventLogEntry in EventLogEntries.OrderBy(e => e.TimeCreated))
            {
                if (eventLogEntry.EventAsEnum == EnumEventID.EventLogStart)
                {
                    startTime = eventLogEntry.TimeCreated;
                    if (!firstStartTime.HasValue)
                        firstStartTime = startTime;
                }

                if (eventLogEntry.EventAsEnum == EnumEventID.EventLogStop)
                {
                    if (startTime.HasValue)
                    {
                        //if (!upTime.HasValue) upTime = new TimeSpan(0);
                        //upTime += eventLogEntry.TimeCreated - startTime.Value;
                        startTime = null;
                    }
                    lastEndTime = eventLogEntry.TimeCreated;
                }
            }

            _start = firstStartTime;
            _end = lastEndTime;
            _totalUpTime = lastEndTime - firstStartTime;
            _consultantBreakTime = Singleton<Settings.Settings>.Instance.DefaultConsultantBreakTime;

            if (_start == null || _end == null)
                _overDay = true;

            NotifyOfPropertyChange(() => Day);
            NotifyOfPropertyChange(() => Start);
            NotifyOfPropertyChange(() => End);
            NotifyOfPropertyChange(() => TotalUpTime);
            NotifyOfPropertyChange(() => OverDay);
            NotifyOfPropertyChange(() => ConsultantStart);
            NotifyOfPropertyChange(() => ConsultantEnd);
            NotifyOfPropertyChange(() => ConsultantBreakTime);
            NotifyOfPropertyChange(() => ConsultantUpTime);
            NotifyOfPropertyChange(() => IsReadOnlyConsultantStart);
            NotifyOfPropertyChange(() => IsReadOnlyConsultantEnd);
        }

        public bool IsEmptyDate => _isEmptyDate;

        public DateTime? Day
        {
            get => _day;
            set => _day = value;
        }

        public DateTime? Start => _start;

        public DateTime? End => _end;

        public TimeSpan? TotalUpTime => _totalUpTime;

        public bool IsManuallyEdited => _consultantStart.HasValue || _consultantEnd.HasValue;

        public bool IsReadOnlyConsultantStart => _start.HasValue || IsEmptyDate;
        public bool IsReadOnlyConsultantEnd => _end.HasValue || IsEmptyDate;
        public bool IsReadOnlyConsultantBreakTime => IsEmptyDate;

        public bool IsWeekEndDay => Day.HasValue &&
            (Day.Value.DayOfWeek == DayOfWeek.Saturday ||
             Day.Value.DayOfWeek == DayOfWeek.Sunday);

        public DateTime? ConsultantStart
        {
            get
            {
                if (_consultantStart.HasValue) return _consultantStart;

                if (_start.HasValue)
                {
                    var realMinutes = _start.Value.Minute + _start.Value.Second / 60.0;
                    var consultandMinutes = RoundConsultantMinutes(realMinutes);
                    if(consultandMinutes < 60)
                        return new DateTime(_start.Value.Year, _start.Value.Month, _start.Value.Day, _start.Value.Hour, consultandMinutes, 0);
                    return new DateTime(_start.Value.Year, _start.Value.Month, _start.Value.Day, _start.Value.Hour + 1, 0, 0);
                }
                return null;
            }
            set
            {
                if (IsReadOnlyConsultantStart) return;

                if (Day.HasValue && value.HasValue)
                {
                    var realMinutes = value.Value.Minute + value.Value.Second / 60.0;
                    var consultandMinutes = RoundConsultantMinutes(realMinutes);

                    _consultantStart = new DateTime(Day.Value.Year, Day.Value.Month, Day.Value.Day, value.Value.Hour, consultandMinutes, 0);

                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(() => ConsultantUpTime);
                    RaiseCalculationHasChangedEvent();
                }
            }
        }

        public DateTime? ConsultantEnd
        {
            get
            {
                if (_consultantEnd.HasValue) return _consultantEnd;

                if (_end.HasValue)
                {
                    var realMinutes = _end.Value.Minute + _end.Value.Second / 60.0;
                    var consultandMinutes = RoundConsultantMinutes(realMinutes);
                    if (consultandMinutes < 60)
                        return new DateTime(_end.Value.Year, _end.Value.Month, _end.Value.Day, _end.Value.Hour, consultandMinutes, 0);
                    return new DateTime(_end.Value.Year, _end.Value.Month, _end.Value.Day, _end.Value.Hour + 1, 0, 0);
                }
                return null;
            }
            set
            {
                if (IsReadOnlyConsultantEnd) return;

                if (Day.HasValue && value.HasValue)
                {
                    var realMinutes = value.Value.Minute + value.Value.Second / 60.0;
                    var consultandMinutes = RoundConsultantMinutes(realMinutes);

                    _consultantEnd = new DateTime(Day.Value.Year, Day.Value.Month, Day.Value.Day, value.Value.Hour, consultandMinutes, 0);

                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(() => ConsultantUpTime);
                    RaiseCalculationHasChangedEvent();
                }
            }
        }

        public TimeSpan? ConsultantUpTime
        {
            get
            {
                if (ConsultantStart.HasValue && ConsultantEnd.HasValue)
                {
                    var time = ConsultantEnd - ConsultantStart;
                    if (ConsultantBreakTime.HasValue)
                        time = time - _consultantBreakTime;
                    if(time.HasValue && time.Value.TotalSeconds < 0)
                        return new TimeSpan(0);
                    return time;
                }
                return null;
            }
        }

        public TimeSpan? ConsultantBreakTime
        {
            get => _consultantBreakTime ?? Singleton<Settings.Settings>.Instance.DefaultConsultantBreakTime;
            set
            {
                if (_consultantBreakTime != value)
                {
                    var consultandMinutes = value ?? new TimeSpan();

                    if (value.HasValue)
                    {
                        var realMinutes = value.Value.Hours * 60 + value.Value.Minutes + value.Value.Seconds / 60.0;
                        consultandMinutes = TimeSpan.FromMinutes(RoundConsultantMinutes(realMinutes));
                    }

                    _consultantBreakTime = consultandMinutes;

                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(()=> ConsultantUpTime);
                    RaiseCalculationHasChangedEvent();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    NotifyOfPropertyChange();
                    RaiseCalculationHasChangedEvent();
                }
            }
        }

        private int RoundConsultantMinutes(double realMinutesAndSeconds)
        {
            if (Math.Abs(realMinutesAndSeconds) < 0.00000001) return 0;
            if (realMinutesAndSeconds > 0 && realMinutesAndSeconds < 7.5) return 0;
            if (realMinutesAndSeconds >= 7.5 && realMinutesAndSeconds < 22.5) return 15;
            if (realMinutesAndSeconds >= 22.5 && realMinutesAndSeconds < 37.5) return 30;
            if (realMinutesAndSeconds >= 37.5 && realMinutesAndSeconds < 52.5) return 45;
            return 60;
        }

        public bool OverDay => _overDay;

        
        private void RaiseCalculationHasChangedEvent()
        {
            if(!_raiseCalculationHasChangedDisabled)
                CalculationsHasChanged?.Invoke(this, null);
        }

        #region IDataErrorInfo
        public string this[string columnName]
        {
            get
            {
                string error = String.Empty;

                switch (columnName)
                {
                    case nameof(ConsultantBreakTime):
                        error = EvaluateBreakTime();
                        break;
                    case nameof(ConsultantUpTime):
                        error = EvaluateUpTime();
                        break;
                }

                return error;
            }
        }

        public bool IsValid => IsBreakTimeValid && IsUpTimeValid;

        public bool IsBreakTimeValid => EvaluateBreakTime() == String.Empty;

        public bool IsUpTimeValid => EvaluateUpTime() == String.Empty;

        private string EvaluateBreakTime()
        {
            var result = String.Empty;
            if (ConsultantBreakTime.HasValue && ConsultantUpTime.HasValue)
            {
                if (ConsultantUpTime > TimeSpan.FromHours(6) && ConsultantUpTime <= TimeSpan.FromHours(9))
                    if (ConsultantBreakTime < TimeSpan.FromMinutes(30))
                        result = "Bei mehr als 6 und weniger als 9 Stunden Arbeitszeit beträgt die minimale Pausenzeit 30 Minuten!";
                if (ConsultantUpTime > TimeSpan.FromHours(9))
                    if (ConsultantBreakTime < TimeSpan.FromMinutes(45))
                        result = "Bei mehr als 9 Stunden Arbeitszeit beträgt die minimale Pausenzeit 45 Minuten!";
            }
            return result;
        }

        private string EvaluateUpTime()
        {
            var result = String.Empty;
            if (ConsultantUpTime.HasValue)
            {
                if (ConsultantUpTime > TimeSpan.FromHours(10))
                    result = "Die tägliche Arbeitszeit darf 10 Stunden nicht überschreiten!";
            }
            return result;
        }

        public string Error { get; }
        #endregion
    }
}