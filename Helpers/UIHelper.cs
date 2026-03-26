using RecordClient.ViewModels;
using RecordClient.Views;
using RecordClient.Views.Controls;
using System.Windows;

namespace RecordClient.Helpers
{
    /// <summary>
    /// UI 요소 식별용 enum (View 또는 ViewModel)
    /// </summary>
    public enum ViewKey
    {
        MainWindow,
        MainPage,
        MainPageViewModel,
        LoginPage,
        LoginPageViewModel
    }

    /// <summary>
    /// WPF UI 스레드에서 안전하게 View/ViewModel을 호출하기 위한 헬퍼 클래스
    /// </summary>
    public static class UIHelper
    {
        /// <summary>
        /// ViewModel을 UI 스레드에서 실행
        /// </summary>
        /// <typeparam name="ViewModel"></typeparam>
        /// <param name="action"></param>
        public static void RunVm<TViewModel>(Action<TViewModel> action)
        {
            Run(GetKey<TViewModel>(), action);
        }

        /// <summary>
        /// View(Page)를 UI 스레드에서 실행
        /// </summary>
        /// <typeparam name="View"></typeparam>
        /// <param name="action"></param>
        public static void RunView<TView>(Action<TView> action) where TView : class
        {
            Run(GetKey<TView>(), obj =>
            {
                if (obj is TView view)
                    action(view);
                else
                    Logger.Warn($"[{typeof(TView).Name}] 형변환 실패");
            });
        }

        /// <summary>
        /// ViewModel을 UI 스레드에서 실행하고 결과 반환,
        /// 두번째 제너릭 파라미터로 반환 타입을 결정합니다.
        /// </summary>
        /// <typeparam name="ViewModel"></typeparam>
        /// <typeparam name="Result"></typeparam>
        /// <param name="func"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static TResult CallVm<TViewModel, TResult>(Func<TViewModel, TResult> func, TResult fallback)
        {
            return Call(GetKey<TViewModel>(), func, fallback);
        }

        /// <summary>
        /// View(Page)를 UI 스레드에서 실행하고 결과 반환,
        /// 두번째 제너릭 파라미터로 반환 타입을 결정합니다.
        /// </summary>
        /// <typeparam name="View"></typeparam>
        /// <typeparam name="Result"></typeparam>
        /// <param name="func"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static TResult CallView<TView, TResult>(Func<TView, TResult> func, TResult fallback) where TView : class
        {
            return Call(GetKey<TView>(), obj =>
            {
                if (obj is TView view)
                    return func(view);
                Logger.Warn($"[{typeof(TView).Name}] 형변환 실패");
                return fallback;
            }, fallback);
        }

        /// <summary>
        /// 타입에 따라 ViewKey 매핑 반환 (MainPage, LoginPage 등)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static ViewKey GetKey<T>()
        {
            return typeof(T).Name switch
            {
                nameof(MainPage) => ViewKey.MainPage,
                nameof(MainPageViewModel) => ViewKey.MainPageViewModel,
                nameof(LoginPage) => ViewKey.LoginPage,
                nameof(MainWindow) => ViewKey.MainWindow,
                // nameof(LoginPageViewModel) => ViewKey.LoginPageViewModel,
                _ => throw new InvalidOperationException($"지원되지 않는 타입: {typeof(T).Name}")
            };
        }

        /// <summary>
        /// ViewModel을 UI 스레드에서 실행하고 결과 반환
        /// </summary>
        /// <typeparam name="ViewModel"></typeparam>
        /// <typeparam name="Result"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        private static TResult Call<TViewModel, TResult>(ViewKey key, Func<TViewModel, TResult> func, TResult fallback)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    Logger.Warn("Dispatcher가 null입니다.");
                    return fallback;
                }

                TResult result = fallback;

                void Execute()
                {
                    var vmObj = TryGetViewModel(key);
                    if (vmObj is TViewModel vm)
                        result = func(vm);
                    else
                        Logger.Warn($"[{key}] ViewModel 접근 실패 또는 타입 불일치");
                }

                if (dispatcher.CheckAccess())
                    Execute();
                else
                    dispatcher.Invoke(Execute);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"UI 호출 실패: {ex.Message}");
                return fallback;
            }
        }


        /// <summary>
        /// View 또는 ViewModel을 UI 스레드에서 실행하고 결과 반환 (object 기반)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        private static T Call<T>(ViewKey key, Func<object, T> func, T fallback)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    Logger.Warn("Dispatcher가 null입니다.");
                    return fallback;
                }

                T result = fallback;

                void Execute()
                {
                    var obj = TryGetViewModel(key);
                    if (obj != null)
                        result = func(obj);
                    else
                        Logger.Warn($"[{key}] ViewModel 접근 실패");
                }

                if (dispatcher.CheckAccess())
                    Execute();
                else
                    dispatcher.Invoke(Execute);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"UI 호출 실패: {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// ViewModel을 UI 스레드에서 실행
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        /// <param name="key"></param>
        /// <param name="action"></param>
        private static void Run<TViewModel>(ViewKey key, Action<TViewModel> action)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    Logger.Warn("Dispatcher가 null입니다.");
                    return;
                }

                void Execute()
                {
                    var vmObj = TryGetViewModel(key);
                    if (vmObj is TViewModel vm)
                        action(vm);
                    else
                        Logger.Warn($"[{key}] ViewModel 접근 실패 또는 타입 불일치");
                }

                if (dispatcher.CheckAccess())
                    Execute();
                else
                    dispatcher.Invoke(Execute);
            }
            catch (Exception ex)
            {
                Logger.Error($"UI 호출 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// View 또는 ViewModel을 UI 스레드에서 실행 (object 기반)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        private static void Run(ViewKey key, Action<object> action)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    Logger.Warn("Dispatcher가 null입니다.");
                    return;
                }

                void Execute()
                {
                    var obj = TryGetViewModel(key);
                    if (obj != null)
                        action(obj);
                    else
                        Logger.Warn($"[{key}] ViewModel 접근 실패");
                }

                if (dispatcher.CheckAccess())
                    Execute();
                else
                    dispatcher.Invoke(Execute);
            }
            catch (Exception ex)
            {
                Logger.Error($"UI 호출 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// ViewKey 기반으로 현재 MainWindow의 MainContentHost에서 ViewModel 또는 View 객체 반환
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static object? TryGetViewModel(ViewKey key)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return null;

            var currentPage = mainWindow.MainContentHost.Content;

            return key switch
            {
                ViewKey.MainPage => currentPage as MainPage,
                ViewKey.MainPageViewModel => (currentPage as MainPage)?.DataContext,
                ViewKey.LoginPage => currentPage as LoginPage,
                ViewKey.LoginPageViewModel => (currentPage as LoginPage)?.DataContext,
                ViewKey.MainWindow => mainWindow,
                _ => null
            };
        }
    }
}
