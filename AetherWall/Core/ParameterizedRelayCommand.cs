using System;
using System.Windows.Input;

namespace AetherWall.Core
{
    public class ParameterizedRelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        public ParameterizedRelayCommand(
            Action<T> execute,
            Func<T, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (parameter is T value)
                return _canExecute?.Invoke(value) ?? true;

            return false;
        }

        public void Execute(object? parameter)
        {
            if (parameter is T value)
                _execute(value);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}