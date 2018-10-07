using System;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using TDC.Tools.ProjectTimer.Tools;
using TDC.Tools.ProjectTimer.ViewModels;

namespace TDC.Tools.ProjectTimer.Validation
{
    [ContentProperty("IsActive")]
    public class BreakTimeValidationRule : ValidationRule
    {
        public BoolDependencyObject IsActive { get; set; }

        public BreakTimeValidationRule()
        {
            IsActive = new BoolDependencyObject { Value = true };
        }

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (IsActive.Value.HasValue && IsActive.Value.Value)
            {
                DailyTimeViewModel vm = (value as BindingGroup).Items[0] as DailyTimeViewModel;
                if (vm?.ConsultantUpTime != null && vm?.ConsultantBreakTime != null)
                {
                    if (vm.ConsultantUpTime > TimeSpan.FromHours(6) && vm.ConsultantUpTime <= TimeSpan.FromHours(9))
                        if (vm.ConsultantBreakTime < TimeSpan.FromMinutes(30))
                            return new ValidationResult(false,
                                "Bei mehr als 6 und weniger als 9 Stunden Arbeitszeit beträgt die minimale Pausenzeit 30 Minuten!");
                    if (vm.ConsultantUpTime > TimeSpan.FromHours(9))
                        if (vm.ConsultantBreakTime < TimeSpan.FromMinutes(45))
                            return new ValidationResult(false,
                                "Bei mehr als 9 Stunden Arbeitszeit beträgt die minimale Pausenzeit 45 Minuten!");
                }
            }
            return ValidationResult.ValidResult;
        }
    }
}