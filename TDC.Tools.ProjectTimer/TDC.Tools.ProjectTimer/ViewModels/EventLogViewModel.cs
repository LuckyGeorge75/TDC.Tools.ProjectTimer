using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Xps.Packaging;
using System.Xml.Serialization;
using Caliburn.Micro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using TDC.Tools.ProjectTimer.Enums;
using TDC.Tools.ProjectTimer.Tools;
using Action = System.Action;

namespace TDC.Tools.ProjectTimer.ViewModels
{
    [Export(typeof(AppViewModel))]
    public class EventLogViewModel : Screen
    {
        private readonly IWindowManager _windowManager;
        private DateTime _eventLogStartTime;
        private DateTime _eventLogEndTime;
        private bool _isBusy;
        private string _months;
        private double _sumWorkingTimes;
        private double _sumBreakTimes;
        private List<EventLogEntryDto> _entries;
        private List<DailyConsultantTimeDto> _consultantTimes = new List<DailyConsultantTimeDto>();
        private XpsDocument _helpDocument;
        private Uri _helpPackageUri;
        private Package _helpDocumentPackage;
        private Stream _helpResourceStream;

        private readonly SemaphoreSlim _consultantFileLocker = new SemaphoreSlim(1,10);
        private readonly SemaphoreSlim _eventlogFileLocker = new SemaphoreSlim(1, 10);
        private double _sumRealTimes;

        #region ctor
        public EventLogViewModel()
        {
            base.DisplayName = "Design Time Object";
        }

        [ImportingConstructor]
        public EventLogViewModel(IWindowManager windowManager)
        {
            _windowManager = windowManager;
            base.DisplayName = "Evaluate Eventlog";
            _eventLogStartTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _eventLogEndTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month), 23, 59, 59);

            var loadAction = new Action(async () =>
            {
                if (File.Exists(Singleton<Settings.Settings>.Instance.EventLogFile))
                    _entries = await LoadEventLogEntriesFromXml();
                if (File.Exists(Singleton<Settings.Settings>.Instance.ConsultantTimesFile))
                    _consultantTimes = await LoadConsultanTimesFromXml();

                await Refresh(false);
            });
            loadAction.BeginInvoke(null, null);
        }
        #endregion

        #region Properties
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public bool IsValidatingTimes => Singleton<Settings.Settings>.Instance.ShowTimeWarnings;

        public DateTime EventLogStartTime
        {
            get => _eventLogStartTime;
            set
            {
                if (_eventLogStartTime != value)
                {
                    _eventLogStartTime = value;
                    NotifyOfPropertyChange();

                    if (_eventLogStartTime <= _eventLogEndTime)
                    {
                        var refreshAction = new Action(async () => await Refresh(false));
                        refreshAction.Invoke();
                    }
                }
            }
        }

        public DateTime EventLogEndTime
        {
            get => _eventLogEndTime;
            set
            {
                if (_eventLogEndTime != value)
                {
                    _eventLogEndTime = value;
                    NotifyOfPropertyChange();

                    if (_eventLogStartTime <= _eventLogEndTime)
                    {
                        var refreshAction = new Action(async () => await Refresh(false));
                        refreshAction.Invoke();
                    }
                }
            }
        }

        public bool TimeSelectionIsVisible => _helpDocument == null;

        public string Months => _months.Trim();

        public double SumWorkingTimes => _sumWorkingTimes;

        public double SumBreakTimes => _sumBreakTimes;

        public double RoundError => SumRealTimes - SumWorkingTimes;

        public double SumRealTimes => _sumRealTimes - _sumBreakTimes;

        public ObservableRangeCollection<DailyTimeViewModel> DailyTimes { get; } = new ObservableRangeCollection<DailyTimeViewModel>();

        public FixedDocumentSequence HelpDocumentSequence => _helpDocument?.GetFixedDocumentSequence();

        #endregion

        #region Commands
        public bool CanEvaluateEventLogCommand => _helpDocument == null;

        public async void EvaluateEventLogCommand()
        {
            Execute.BeginOnUIThread(() => IsBusy = true);
            //await EvaluateEventLog();
            await EvaluateEventLogWithLogReader();
            await Refresh(false);
            Execute.BeginOnUIThread(() => IsBusy = false);
        }

        public bool CanExportToExcelCommand => _helpDocument == null;

        public async void ExportToExcelCommand()
        {
            if (DailyTimes.Any(dt => !dt.IsValid) && Singleton<Settings.Settings>.Instance.ShowTimeWarnings)
            {
                var metroWindow = (Application.Current.MainWindow as MetroWindow);
                var result = await metroWindow.ShowMessageAsync("Warnung", "Die Liste enthält ungültige Zeitangaben!\r\nWollen Sie fortfahren?", MessageDialogStyle.AffirmativeAndNegative);
                if (result == MessageDialogResult.Negative)
                    return;
            }


            var sfd = new SaveFileDialog()
            {
                RestoreDirectory = true,
                Filter = "Excel Worksheets|*.xlsx",
                FileName = "ProjectTimes_" + Months.Replace(' ', '_') + ".xlsx",
                OverwritePrompt = true,
            };
            var sfdResult = sfd.ShowDialog();
            if (sfdResult.HasValue && sfdResult.Value)
            {
                try
                {


                    var newFile = new FileInfo(sfd.FileName);
                    if (newFile.Exists)
                    {
                        newFile.Delete(); // ensures we create a new workbook
                        newFile = new FileInfo(sfd.FileName);
                    }

                    using (var package = new ExcelPackage(newFile))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(Months);

                        if(!String.IsNullOrEmpty(Months) && Months.Contains(' '))
                            worksheet.Cells[1, 1].Value = "Zeiten aus Eventlog für die Monate " + Months;
                        else
                            worksheet.Cells[1, 1].Value = "Zeiten aus Eventlog für den Monat " + Months;
                        worksheet.Cells[1, 1, 1, 9].Merge = true;
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.CenterContinuous;

                        worksheet.Cells[3, 1].Value = "Tag";
                        worksheet.Cells[3, 2].Value = "Start (Eventlog)";
                        worksheet.Cells[3, 3].Value = "Ende (Eventlog)";
                        worksheet.Cells[3, 4].Value = "Zeit (Eventlog)";
                        worksheet.Cells[3, 5].Value = "Start (Consultant)";
                        worksheet.Cells[3, 6].Value = "Ende (Consultant)";
                        worksheet.Cells[3, 7].Value = "Pause (Consultant)";
                        worksheet.Cells[3, 8].Value = "Zeit (Consultant)";
                        worksheet.Cells[3, 9].Value = "Beschreibung";

                        var valuesStart = 4;
                        var count = valuesStart;
                        foreach (var dailyTime in DailyTimes)
                        {
                            worksheet.Cells[count, 1].Value = dailyTime.Day;
                            worksheet.Cells[count, 1].Style.Numberformat.Format = "dd.MM. yyyy";
                            if (!dailyTime.IsEmptyDate)
                            {
                                worksheet.Cells[count, 2].Value = dailyTime.Start?.TimeOfDay ?? new TimeSpan(0);
                                worksheet.Cells[count, 2].Style.Numberformat.Format = "HH:mm:ss";
                                worksheet.Cells[count, 3].Value = dailyTime.End?.TimeOfDay ?? new TimeSpan(0);
                                worksheet.Cells[count, 3].Style.Numberformat.Format = "HH:mm:ss";
                                worksheet.Cells[count, 4].Value = dailyTime.TotalUpTime;
                                worksheet.Cells[count, 4].Style.Numberformat.Format = "HH:mm:ss";
                                worksheet.Cells[count, 5].Value = dailyTime.ConsultantStart?.TimeOfDay ?? new TimeSpan(0);
                                worksheet.Cells[count, 5].Style.Numberformat.Format = "HH:mm";
                                worksheet.Cells[count, 6].Value = dailyTime.ConsultantEnd?.TimeOfDay ?? new TimeSpan(0);
                                worksheet.Cells[count, 6].Style.Numberformat.Format = "HH:mm";
                                worksheet.Cells[count, 7].Value = dailyTime.ConsultantBreakTime;
                                worksheet.Cells[count, 7].Style.Numberformat.Format = "HH:mm";
                                worksheet.Cells[count, 8].Value = dailyTime.ConsultantUpTime.HasValue && dailyTime.ConsultantUpTime.Value.Ticks > 0 ? dailyTime.ConsultantUpTime : new TimeSpan(0);
                                worksheet.Cells[count, 8].Style.Numberformat.Format = "[hh]:mm";
                            }
                            worksheet.Cells[count, 9].Value = dailyTime.Description;
                            count++;
                        }

                        worksheet.Cells[count + 1, 3].Value = "Summe";
                        worksheet.Cells[count + 1, 3].Style.Font.Bold = true;
                        worksheet.Cells[count + 1, 4].Formula = $"SUM(D{valuesStart}:D{count})";
                        worksheet.Cells[count + 1, 4].Style.Font.Bold = true;
                        worksheet.Cells[count + 1, 4].Style.Numberformat.Format = "[hh]:mm";
                        worksheet.Cells[count + 1, 7].Formula = $"SUM(G{valuesStart}:G{count})";
                        worksheet.Cells[count + 1, 7].Style.Font.Bold = true;
                        worksheet.Cells[count + 1, 7].Style.Numberformat.Format = "[hh]:mm";
                        worksheet.Cells[count + 1, 8].Formula = $"SUM(H{valuesStart}:H{count})";
                        worksheet.Cells[count + 1, 8].Style.Font.Bold = true;
                        worksheet.Cells[count + 1, 8].Style.Numberformat.Format = "[hh]:mm";
                        


                        worksheet.Cells[2, 11].Value = "Zusammenfassung (Beraterzeiten)";
                        worksheet.Cells[2, 11].Style.Font.Bold = true;
                        worksheet.Cells[2, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.CenterContinuous;
                        worksheet.Cells[2, 11, 2, 13].Merge = true;
                        worksheet.Cells[4, 11].Value = "Monat(e)";
                        worksheet.Cells[5, 11].Value = "Zeit (Berater)";
                        worksheet.Cells[6, 11].Value = "Pause";
                        worksheet.Cells[7, 11].Value = "Zeit (Eventlog abzgl. Pause)";
                        worksheet.Column(11).Width = 12.0;
                        worksheet.Column(12).Width = 12.0;
                        worksheet.Column(13).Width = 16.0;

                        worksheet.Cells[4, 12].Value = Months;
                        worksheet.Cells[4, 12].Style.Font.Bold = true;
                        worksheet.Cells[5, 12].Value = SumWorkingTimes;
                        worksheet.Cells[5, 12].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[5, 12].Style.Font.Bold = true;
                        worksheet.Cells[6, 12].Value = SumBreakTimes;
                        worksheet.Cells[6, 12].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[6, 12].Style.Font.Bold = true;
                        worksheet.Cells[7, 12].Value = SumRealTimes;
                        worksheet.Cells[7, 12].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[7, 12].Style.Font.Bold = true;

                        worksheet.Cells[5, 13].Value = "Industriestunden";
                        worksheet.Cells[6, 13].Value = "Industriestunden";
                        worksheet.Cells[7, 13].Value = "Industriestunden";

                        worksheet.Cells[1, 1, count, 9].AutoFitColumns(0);
                        worksheet.Cells[3, 1, 3, 9].Style.Border.BorderAround(ExcelBorderStyle.Thick);
                        worksheet.Cells[4, 1, count - 1, 9].Style.Border.BorderAround(ExcelBorderStyle.Thick);
                        worksheet.Cells[4, 11, 7, 13].Style.Border.BorderAround(ExcelBorderStyle.Thick);
                        worksheet.Cells[count + 1, 3, count + 1, 9].Style.Border.BorderAround(ExcelBorderStyle.Thick);

                        package.Workbook.Properties.Title = "Projektzeiten";
                        package.Workbook.Properties.Author = "ProjectTimer Tool by Daniel Winkler";
                        package.Save();

                        Type officeType = Type.GetTypeFromProgID("Excel.Application");
                        if (officeType == null)
                        {
                            MessageBox.Show($"Die Exceldatei {newFile.FullName} wurde erzeugt!", "Info", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            Process.Start(newFile.FullName);
                        }
                    }

                }
                catch (Exception e)
                {
                    MessageBox.Show("Konnte Exceldatei nicht erzeugen.", "Fehler", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public void ShowHelpCommand()
        {
            if (_helpDocument != null)
            {
                PackageStore.RemovePackage(_helpPackageUri);

                _helpDocument?.Close();
                _helpDocumentPackage?.Close();
                _helpDocument = null;
                _helpResourceStream?.Dispose();

                Execute.OnUIThread(() =>
                {
                    Execute.OnUIThread(() => NotifyOfPropertyChange(() => HelpDocumentSequence));
                    Execute.OnUIThread(() => NotifyOfPropertyChange(() => CanEvaluateEventLogCommand));
                    Execute.OnUIThread(() => NotifyOfPropertyChange(() => CanExportToExcelCommand));
                    Execute.OnUIThread(() => NotifyOfPropertyChange(() => TimeSelectionIsVisible));
                });
                return;
            }
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TDC.Tools.ProjectTimer.Resources.ProjectTimer.xps";

            _helpResourceStream = assembly.GetManifestResourceStream(resourceName);

            if (_helpResourceStream != null)
            {
                _helpDocumentPackage = Package.Open(_helpResourceStream);

                string strMemoryPackageName = $"memorystream://{resourceName}.xps";
                _helpPackageUri = new Uri(strMemoryPackageName);

                PackageStore.AddPackage(_helpPackageUri, _helpDocumentPackage);

                _helpDocument = new XpsDocument(_helpDocumentPackage, CompressionOption.Maximum, strMemoryPackageName);
            }

            Execute.OnUIThread(() => NotifyOfPropertyChange(() => HelpDocumentSequence));
            Execute.OnUIThread(() => NotifyOfPropertyChange(() => CanEvaluateEventLogCommand));
            Execute.OnUIThread(() => NotifyOfPropertyChange(() => CanExportToExcelCommand));
            Execute.OnUIThread(() => NotifyOfPropertyChange(() => TimeSelectionIsVisible));

        }
        #endregion

        public async Task EvaluateEventLog()
        {
            await Task.Run(async () =>
            {
                var entryViewModels = new List<EventLogEntryDto>();

                using (EventLog eventLog = new EventLog("System", Environment.MachineName))
                {

                    var logEntries = eventLog.Entries.Cast<EventLogEntry>();

                    var filteredLogEntries = logEntries
                        .Where(e => (UInt16) e.InstanceId == (UInt16) EnumEventID.EventLogStart ||
                                    (UInt16) e.InstanceId == (UInt16) EnumEventID.EventLogStop).AsParallel().ToList();

                    foreach (var filteredLogEntry in filteredLogEntries)
                    {
                        entryViewModels.Add(EventLogEntryDto.CreateInstance(filteredLogEntry));
                    }
                }
                await SaveEventLogEntriesAsXml(entryViewModels);
                _entries = await LoadEventLogEntriesFromXml();
            });
        }

        public async Task EvaluateEventLogWithLogReader()
        {
            await Task.Run(async () =>
            {
                var entryViewModels = new List<EventLogEntryDto>();

                string query = $"*[System/EventID={(UInt16)EnumEventID.EventLogStart} or System/EventID={(UInt16)EnumEventID.EventLogStop}]";
                EventLogQuery eventsQuery = new EventLogQuery("System", PathType.LogName, query);
                EventLogReader logReader = new EventLogReader(eventsQuery);

                for (EventRecord eventdetail = logReader.ReadEvent(); eventdetail != null; eventdetail = logReader.ReadEvent())
                {
                    entryViewModels.Add(EventLogEntryDto.CreateInstance(eventdetail));
                }

                await SaveEventLogEntriesAsXml(entryViewModels);
                _entries = await LoadEventLogEntriesFromXml();
            });
        }

        public async Task Refresh(bool saveConsultantTimes)
        {
            Execute.BeginOnUIThread(() => IsBusy = true);
            Execute.BeginOnUIThread(() => NotifyOfPropertyChange(nameof(IsValidatingTimes)));
            UpdateDailyEntries();
            MapConsultantTimes();
            await UpdateSummary(saveConsultantTimes);
            Execute.BeginOnUIThread(() => IsBusy = false);
        }

        private void UpdateDailyEntries()
        {
            var dailyTimeViewModels = new List<DailyTimeViewModel>();

            if (_entries != null)
            {
                //foreach (var vm in _entries)
                //{
                //    if (DateTime.Compare(vm.TimeCreated, EventLogStartTime) >= 0 &&
                //        DateTime.Compare(vm.TimeCreated, EventLogEndTime.AddDays(1)) <= 0)
                //    {
                //        var existingEntry = dailyTimeViewModels.FirstOrDefault(dt => dt.Day.HasValue &&
                //                                                            dt.Day.Value.Year == vm.TimeCreated.Year &&
                //                                                            dt.Day.Value.Month == vm.TimeCreated
                //                                                                .Month &&
                //                                                            dt.Day.Value.Day == vm.TimeCreated.Day);
                //        if (existingEntry == null)
                //        {
                //            existingEntry = new DailyTimeViewModel();
                //            dailyTimeViewModels.Add(existingEntry);
                //        }
                //        existingEntry.Add(vm);
                //    }
                //}

                for (var date = EventLogStartTime.Date; date <= EventLogEndTime.Date; date = date.AddDays(1.0))
                {
                    var eventLogEntries = _entries.Where(ele => ele.TimeCreated.Date == date).ToList();
                    if (eventLogEntries.Any())
                    {
                        var dtViewModel = new DailyTimeViewModel();
                        foreach (var entry in eventLogEntries)
                        {
                            dtViewModel.Add(entry);
                        }
                        dailyTimeViewModels.Add(dtViewModel);
                    }
                    else if (Singleton<Settings.Settings>.Instance.ShowEmptyDays)
                    {
                        dailyTimeViewModels.Add(new DailyTimeViewModel(true) { Day = date });
                    }
                }
            }

            Execute.OnUIThread(() =>
            {
                foreach (var dailyTime in DailyTimes.Where(dt => !dt.IsEmptyDate))
                {
                    dailyTime.CalculationsHasChanged -= OnCalculationsHasChanged;
                }

                DailyTimes.Clear();
                DailyTimes.AddRange(dailyTimeViewModels);

                foreach (var dailyTime in DailyTimes.Where(dt => !dt.IsEmptyDate))
                {
                    dailyTime.CalculationsHasChanged += OnCalculationsHasChanged;
                }
            });
        }

        private async void OnCalculationsHasChanged(object sender, EventArgs args)
        {
            await UpdateSummary(true);
        }

        private void MapConsultantTimes()
        {
            if (_consultantTimes != null)
            {
                foreach (var dailyTime in DailyTimes.Where(dt => !dt.IsEmptyDate))
                {
                    foreach (var consultantTime in _consultantTimes)
                    {
                        if (dailyTime.EventLogEntries.Any(ee => consultantTime.EventLogDtoIds.Contains(ee.Index)))
                        {
                            dailyTime.SetConsultandTimes(consultantTime);
                            break;
                        }
                    }
                }
            }
        }

        private async Task UpdateSummary(bool saveConsultantTimes)
        {
            bool[] monthFlags = new bool[12];

            _sumWorkingTimes = 0.0;
            _sumBreakTimes = 0.0;
            _sumRealTimes = 0.0;
            _months = String.Empty;

            foreach (var dailyTime in DailyTimes.Where(dt => !dt.IsEmptyDate))
            {
                if (dailyTime.Day.HasValue && !monthFlags[dailyTime.Day.Value.Month - 1])
                {
                    monthFlags[dailyTime.Day.Value.Month - 1] = true;
                    _months += $" {dailyTime.Day.Value:MMMM}";
                }

                if (dailyTime.ConsultantUpTime.HasValue)
                    _sumWorkingTimes += dailyTime.ConsultantUpTime.Value.TotalMinutes / 60.0;
                if (dailyTime.ConsultantBreakTime.HasValue)
                    _sumBreakTimes += dailyTime.ConsultantBreakTime.Value.TotalMinutes / 60.0;
                if(dailyTime.TotalUpTime.HasValue)
                    _sumRealTimes += dailyTime.TotalUpTime.Value.TotalMinutes / 60.0;
                else if (dailyTime.ConsultantUpTime.HasValue)
                    _sumRealTimes += dailyTime.ConsultantUpTime.Value.TotalMinutes / 60.0;

                var consultantTimes = dailyTime.GetConsultantTimes();
                if (consultantTimes != null)
                {
                    var existingConsultantTimes = _consultantTimes.FirstOrDefault(ct => ct.Id.Equals(consultantTimes.Id));
                    if (existingConsultantTimes == null)
                        _consultantTimes.Add(consultantTimes);
                    else
                    {
                        existingConsultantTimes.ConsultantBreakTime = consultantTimes.ConsultantBreakTime;
                        existingConsultantTimes.ConsultantStart = consultantTimes.ConsultantStart;
                        existingConsultantTimes.ConsultantEnd = consultantTimes.ConsultantEnd;
                        existingConsultantTimes.EventLogDtoIds = consultantTimes.EventLogDtoIds;
                        existingConsultantTimes.Description = consultantTimes.Description;
                    }
                }
            }

            //GetRound Error

            if(saveConsultantTimes)
                await SaveConsultanTimesAsXml(_consultantTimes);

            Execute.BeginOnUIThread(() =>
            {
                NotifyOfPropertyChange(nameof(Months));
                NotifyOfPropertyChange(nameof(SumWorkingTimes));
                NotifyOfPropertyChange(nameof(SumBreakTimes));
                NotifyOfPropertyChange(nameof(RoundError));
                NotifyOfPropertyChange(nameof(SumRealTimes));
            });
        }

        #region Loading and Saving
        private async Task SaveConsultanTimesAsXml(List<DailyConsultantTimeDto> entryList)
        {
            await _consultantFileLocker.WaitAsync();

            try
            {
                if (entryList != null)
                {
                    Execute.BeginOnUIThread(() => IsBusy = true);

                    if (entryList.Any(e => e.Id.Equals(Guid.Empty)))
                        throw new ArgumentException();

                    await Task.Run(() =>
                    {
                        XmlSerializer xs =
                            new XmlSerializer(typeof(object), new[] {typeof(List<DailyConsultantTimeDto>)});
                        using (StreamWriter streamWriter =
                            File.CreateText(Singleton<Settings.Settings>.Instance.ConsultantTimesFile))
                        {
                            xs.Serialize(streamWriter, entryList);
                        }
                    });

                    Execute.BeginOnUIThread(() => IsBusy = false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                _consultantFileLocker.Release();
            }

        }

        private async Task<List<DailyConsultantTimeDto>> LoadConsultanTimesFromXml()
        {
            Execute.BeginOnUIThread(() => IsBusy = true);

            List<DailyConsultantTimeDto> retVal = null;

            await Task.Run(() =>
            {
                try
                {
                    XmlSerializer xs = new XmlSerializer(typeof(object), new[] { typeof(List<DailyConsultantTimeDto>) });
                    using (StreamReader streamReader = new StreamReader(Singleton<Settings.Settings>.Instance.ConsultantTimesFile))
                    {
                        retVal = xs.Deserialize(streamReader) as List<DailyConsultantTimeDto>;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            });

            Execute.BeginOnUIThread(() =>
            {
                IsBusy = false;
            });

            return retVal;
        }

        private async Task SaveEventLogEntriesAsXml(List<EventLogEntryDto> entryList)
        {
            await _eventlogFileLocker.WaitAsync();

            try
            {
                if (entryList != null)
                {
                    Execute.BeginOnUIThread(() => IsBusy = true);

                    await Task.Run(() =>
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(object), new[] {typeof(List<EventLogEntryDto>)});
                        using (StreamWriter streamWriter =
                            File.CreateText(Singleton<Settings.Settings>.Instance.EventLogFile))
                        {
                            xs.Serialize(streamWriter, entryList);
                        }
                    });

                    Execute.BeginOnUIThread(() => IsBusy = false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                _eventlogFileLocker.Release();
            }

        }

        private async Task<List<EventLogEntryDto>> LoadEventLogEntriesFromXml()
        {
            Execute.BeginOnUIThread(() => IsBusy = true);

            List<EventLogEntryDto> retVal = null;

            await Task.Run(() =>
            {
                try
                {
                    XmlSerializer xs = new XmlSerializer(typeof(object), new[] { typeof(List<EventLogEntryDto>) });
                    using (StreamReader streamReader = new StreamReader(Singleton<Settings.Settings>.Instance.EventLogFile))
                    {
                        retVal = xs.Deserialize(streamReader) as List<EventLogEntryDto>;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                
            });

            Execute.BeginOnUIThread(() =>
            {
                IsBusy = false;
            });

            return retVal;
        }

        
        #endregion
    }
}