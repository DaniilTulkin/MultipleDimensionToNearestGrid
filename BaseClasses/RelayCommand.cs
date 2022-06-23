using System;
using System.Windows.Input;

namespace MultipleDimensionToNearestGrid
{
    public class RelayCommandWithoutParameter : ICommand
    {
        private readonly Action _execute;
        private readonly Func<object, bool> _canExecute;
        public string CommandName { get; }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommandWithoutParameter(Action execute, Func<object, bool> canExecute = null)
        {
            this._execute = execute;
            this._canExecute = canExecute;
        }
        public RelayCommandWithoutParameter(
          Action execute,
          string commandName,
          Func<object, bool> canExecute = null)
        {
            this._execute = execute;
            this._canExecute = canExecute;
            this.CommandName = commandName;
        }
        public bool CanExecute(object parameter) => this._canExecute == null || this._canExecute(parameter);

        public void Execute(object parameter) => this._execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public RelayCommand(Action<T> execute, Func<object, bool> canExecute = null)
        {
            this._execute = execute;
            this._canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => this._canExecute == null || this._canExecute(parameter);

        public void Execute(object parameter) => this._execute((T)parameter);
    }
}
