using System;
using System.ComponentModel.Composition;
using System.Dynamic;
using System.Reflection;
using System.Windows;
using Caliburn.Micro;
using TDC.Tools.ProjectTimer.Tools;

namespace TDC.Tools.ProjectTimer.ViewModels
{
    [Export(typeof(AppViewModel))]
    public class AppViewModel : Conductor<object>
    {
        private readonly IWindowManager _windowManager;

        [ImportingConstructor]
        public AppViewModel(IWindowManager windowManager)
        {
            _windowManager = windowManager;
            this.Activated += (sender, args) => ActivateItem(new EventLogViewModel(_windowManager));

            Singleton<Settings.Settings>.Instance.Load();
        }

        public void OpenSettings()
        {
            IsSettingsFlyoutOpen = true;
        }

        private bool _isSettingsFlyoutOpen;

        public bool IsSettingsFlyoutOpen
        {
            get => _isSettingsFlyoutOpen;
            set
            {
                _isSettingsFlyoutOpen = value;
                NotifyOfPropertyChange();
            }
        }

        public string EventLogFile
        {
            get => Singleton<Settings.Settings>.Instance.EventLogFile;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.EventLogFile.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.EventLogFile = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                }
            }
        }

        public string ConsultantTimesFile
        {
            get => Singleton<Settings.Settings>.Instance.ConsultantTimesFile;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.ConsultantTimesFile.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.ConsultantTimesFile = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                }
            }
        }

        public TimeSpan DefaultConsultantStartTime
        {
            get => Singleton<Settings.Settings>.Instance.DefaultConsultantStartTime;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.DefaultConsultantStartTime.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.DefaultConsultantStartTime = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                }
            }
        }

        public TimeSpan DefaultConsultantEndTime
        {
            get => Singleton<Settings.Settings>.Instance.DefaultConsultantEndTime;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.DefaultConsultantEndTime.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.DefaultConsultantEndTime = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                }
            }
        }

        public TimeSpan DefaultConsultantBreakTime
        {
            get => Singleton<Settings.Settings>.Instance.DefaultConsultantBreakTime;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.DefaultConsultantBreakTime.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.DefaultConsultantBreakTime = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                }
            }
        }

        public bool ShowEmptyDays
        {
            get => Singleton<Settings.Settings>.Instance.ShowEmptyDays;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.ShowEmptyDays.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.ShowEmptyDays = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                    if (ActiveItem is EventLogViewModel)
                        ((EventLogViewModel) ActiveItem).Refresh(false);
                }
            }
        }

        public bool ShowTimeWarnings
        {
            get => Singleton<Settings.Settings>.Instance.ShowTimeWarnings;
            set
            {
                if (!Singleton<Settings.Settings>.Instance.ShowTimeWarnings.Equals(value))
                {
                    Singleton<Settings.Settings>.Instance.ShowTimeWarnings = value;
                    Singleton<Settings.Settings>.Instance.Save();
                    NotifyOfPropertyChange();
                    if (ActiveItem is EventLogViewModel)
                        ((EventLogViewModel)ActiveItem).Refresh(false);
                }
            }
        }
    }
}