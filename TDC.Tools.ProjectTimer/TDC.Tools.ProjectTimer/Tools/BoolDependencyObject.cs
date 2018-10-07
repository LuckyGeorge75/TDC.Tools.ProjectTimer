using System.Windows;
using System.Windows.Data;

namespace TDC.Tools.ProjectTimer.Tools
{
    public class BoolDependencyObject : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(bool?),
            typeof(BoolDependencyObject),
            new PropertyMetadata(true, OnValueChanged));

        public static readonly DependencyProperty BindingToTriggerProperty = DependencyProperty.Register(
            nameof(BindingToTrigger),
            typeof(object),
            typeof(BoolDependencyObject),
            new FrameworkPropertyMetadata(default(object), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object BindingToTrigger
        {
            get => GetValue(BindingToTriggerProperty);
            set => SetValue(BindingToTriggerProperty, value);
        }

        public bool? Value
        {
            get => (bool?) GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var comparisonValue = (BoolDependencyObject) d;
            var bindingExpressionBase =
                BindingOperations.GetBindingExpressionBase(comparisonValue, BindingToTriggerProperty);
            bindingExpressionBase?.UpdateSource();
        }
    }
}