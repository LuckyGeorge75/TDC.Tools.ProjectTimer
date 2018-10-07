using System;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using TDC.Tools.ProjectTimer.Tools;
using TDC.Tools.ProjectTimer.ViewModels;

namespace TDC.Tools.ProjectTimer.Validation
{
    [ContentProperty("IsActive")]
    public class UpTimeValidationRule : ValidationRule
    {
        public BoolDependencyObject IsActive { get; set; }

        public UpTimeValidationRule()
        {
            IsActive = new BoolDependencyObject { Value = true };
        }

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (IsActive.Value.HasValue && IsActive.Value.Value)
            {
                DailyTimeViewModel vm = (value as BindingGroup).Items[0] as DailyTimeViewModel;
                if (vm?.ConsultantUpTime != null && vm.ConsultantUpTime > TimeSpan.FromHours(10))
                {
                    return new ValidationResult(false, "Die tägliche Arbeitszeit darf 10 Stunden nicht überschreiten!");
                }
            }
            return ValidationResult.ValidResult;
        }
    }
}