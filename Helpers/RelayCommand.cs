using System.Windows.Input;

namespace RecordClient.Helpers
{
    /// <summary>
    /// 일반 객체 파라미터를 사용하는 커맨드 구현 (object? 사용)
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;                 // 실행할 동작
        private readonly Predicate<object?>? _canExecute;          // 실행 가능 여부 판단 함수

        /// <summary>
        /// object? 기반 생성자
        /// </summary>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 파라미터 없는 Action 기반 생성자
        /// </summary>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));

            _execute = _ => execute(); // object? 파라미터 무시하고 실행
            if (canExecute != null)
                _canExecute = _ => canExecute();
        }

        /// <summary>
        /// 명령 실행 가능 여부 판단
        /// </summary>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <summary>
        /// 명령 실행
        /// </summary>
        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>
        /// CanExecute 변경 감지를 위한 이벤트 (CommandManager 사용)
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    /// <summary>
    /// 제네릭 타입 파라미터를 사용하는 커맨드 구현 (T 사용)
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;               // 실행할 동작
        private readonly Predicate<T>? _canExecute;        // 실행 가능 여부 판단 함수

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 명령 실행 가능 여부 판단
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            if (parameter == null && typeof(T).IsValueType)
                return _canExecute == null;

            return _canExecute?.Invoke((T)parameter!) ?? true;
        }

        /// <summary>
        /// 명령 실행
        /// </summary>
        public void Execute(object? parameter)
        {
            try
            {
                var converted = (T)System.Convert.ChangeType(parameter, typeof(T)); // 형변환 시도
                _execute(converted);
            }
            catch (Exception ex)
            {
                Logger.Error($"RelayCommand 변환 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// CanExecute 변경 감지를 위한 이벤트 (CommandManager 사용)
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
