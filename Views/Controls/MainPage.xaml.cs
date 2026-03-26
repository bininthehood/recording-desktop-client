using RecordClient.Helpers;
using RecordClient.Helpers.InterfaceSocket;
using RecordClient.Models.Device;
using RecordClient.Services;
using RecordClient.ViewModels;
using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using ComboBox = System.Windows.Controls.ComboBox;
using Window = System.Windows.Window;

namespace RecordClient.Views.Controls
{
    public partial class MainPage : UserControl
    {
        private bool _isRenderingConnected = false;
        private Storyboard? _blinkStoryboard;
        private PropertyChangedEventHandler? _vmHandler;


        private readonly MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            InitStoryboard();
            InitPropertyHandler();
            ConnectRendering();
            InterfaceSocket.Instance.Start();
        }
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                if (MainWindow.isWeb)
                {
                    RecordButton.Visibility = Visibility.Hidden;
                    ToggleCommentButton.Visibility = Visibility.Hidden;
                }
                else
                {
                    RecordButton.Visibility = Visibility.Visible;
                    ToggleCommentButton.Visibility = Visibility.Visible;
                }
            }
            else
            {
                Logger.Error("MainWindow 참조 실패 (Window.GetWindow(this) == null)");
            }

            _isInitialized = true;
        }
        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeViewResources();
        }

        private void ConnectRendering()
        {
            if (!_isRenderingConnected)
            {
                CompositionTarget.Rendering -= _viewModel.OnRendering;
                CompositionTarget.Rendering += _viewModel.OnRendering;

                CompositionTarget.Rendering -= UpdateVolumeHandler;
                CompositionTarget.Rendering += UpdateVolumeHandler;

                _isRenderingConnected = true;
            }
        }

        public void DisposeViewResources()
        {
            if (_viewModel != null && _isRenderingConnected)
            {
                // 렌더링 및 기타 이벤트 해제
                if (_vmHandler != null)
                    _viewModel.PropertyChanged -= _vmHandler;

                CompositionTarget.Rendering -= _viewModel.OnRendering;
                CompositionTarget.Rendering -= UpdateVolumeHandler;

                _isRenderingConnected = false;
                Logger.Debug("CompositionTarget.Rendering 연결 해제");


                // ViewModel 자원 해제
                if (_viewModel.IsRecording)
                    _viewModel.StopRecording();

                _viewModel.Dispose();

                // 웹소켓 중지
                InterfaceSocket.Instance.Stop();

                this.DataContext = null;
            }
        }

        private void UpdateVolumeHandler(object? sender, EventArgs e)
        {
            UpdateInputVolumeBar();
        }

        private void UpdateInputVolumeBar()
        {
            double inputMax = (InputLevelSlider.Value) * 150;
            double inputWidth = _viewModel.SmoothedInputVolumeLevel * inputMax; // MaxWidth에 맞춰 계산
            InputVolumeBar.Width = inputWidth;

            double outputMax = (OutputLevelSlider.Value) * 150;
            double outputWidth = _viewModel.SmoothedOutputVolumeLevel * outputMax; // MaxWidth에 맞춰 계산
            OutputVolumeBar.Width = outputWidth;
        }


        private void InitStoryboard()
        {
            _blinkStoryboard = (Storyboard)this.Resources["BlinkStoryboard"];

            if (_viewModel == null)
            {
                Logger.Error("ViewModel이 null입니다.");
                return;
            }
        }
        private void InitPropertyHandler()
        {
            _vmHandler = (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.IsRecording):
                        if (_blinkStoryboard != null)
                        {
                            if (_viewModel.IsRecording)
                            {
                                _blinkStoryboard.Begin();
                                BlinkIcon.Opacity = 1;
                            }
                            else
                            {
                                _blinkStoryboard.Stop();
                                _blinkStoryboard.Remove();
                                BlinkIcon.Opacity = 0;
                            }
                        }
                        break;

                    // ** 2025-07-14 애니메이션 떨림 이슈로 주석 처리
                    case nameof(_viewModel.PlayOpenAnimation):
                        if (_viewModel.PlayOpenAnimation)
                        {
                            int tab = _viewModel.SelectedTabIndex;
                            /*
                            var openStoryboard = (Storyboard)this.Resources[$"SubAreaOpenStoryboard_{tab}"];

                            openStoryboard?.Begin();*/
                            if (tab == 0)
                            {
                                SubArea.Height = 70;
                            }
                            else
                            {
                                SubArea.Height = 160;
                            }

                        }
                        break;

                    case nameof(_viewModel.PlayCloseAnimation):
                        if (_viewModel.PlayCloseAnimation)
                        {
                            /*var closeStoryboard = (Storyboard)this.Resources["SubAreaCloseStoryboard"];
                            closeStoryboard?.Begin();*/

                            SubArea.Height = 0;
                        }
                        break;
                    case nameof(_viewModel.SelectedTabIndex):
                        int _tab = _viewModel.SelectedTabIndex;
                        /*
                        int preTab = _tab == 0 ? 1 : 0;

                        var storyboard = (Storyboard)this.Resources[$"SubAreaOpenStoryboard_{_tab}"];
                        var preStoryboard = (Storyboard)this.Resources[$"SubAreaOpenStoryboard_{preTab}"];

                        preStoryboard?.Remove();
                        storyboard?.Begin();*/

                        if (_tab == 0)
                        {
                            SubArea.Height = 70;
                        }
                        else
                        {
                            SubArea.Height = 160;
                        }

                        break;
                }
            };


            _viewModel.PropertyChanged += _vmHandler;
        }

            

        private async void InputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;

            // 변경된 항목 확인
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is DeviceItem device)
            {
                await _viewModel.SetSelectedInputDeviceAsync(device);
            }
        }

        private async void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is DeviceItem device)
            {
                await _viewModel.SetSelectedOutputDeviceAsync(device);
            }
        }
        private void Output_Button_Click(object sender, RoutedEventArgs e)
        {
            SystemSounds.Beep.Play();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    EnterButton_Click(null!, null!);
                }
            }
        }

        private async void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                await _viewModel.SubmitCommentAsync();
            }
        }

        private void ComboBox_PreviewButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox combo)
            {
                // 마우스 클릭 위치가 드롭다운 항목이 아니면 처리
                DependencyObject? src = e.OriginalSource as DependencyObject;
                while (src != null)
                {
                    if (src is ComboBoxItem) return; // 항목 선택이면 건들지 않음
                    src = VisualTreeHelper.GetParent(src);
                }

                combo.IsDropDownOpen = !combo.IsDropDownOpen;
                e.Handled = true;
            }
        }





        private bool _isInternalChange = false;
        private bool _isDraggingThumb = false;
        private bool _isTrackClick = false;
        private bool _isInitialized = false;

        private Slider? _activeSlider = null;

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                _activeSlider = slider;
                slider.ApplyTemplate();

                if (slider.Template.FindName("PART_Track", slider) is Track track)
                {
                    Point pt = e.GetPosition(track);

                    if (track.Thumb != null && track.Thumb.IsMouseOver)
                    {
                        // Thumb 드래그 시작
                        _isDraggingThumb = true;
                        _isTrackClick = false;
                        _isInternalChange = true; // 드래그 동안 ValueChanged 무시
                    }
                    else
                    {
                        // Track 클릭 처리
                        double ratio = pt.X / track.ActualWidth;
                        double newValue = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;

                        _isTrackClick = true;
                        _isDraggingThumb = false;

                        _isInternalChange = true;
                        slider.Value = newValue;
                        _isInternalChange = false;

                        CommitSliderValue(slider, newValue); // 여기서만 1회 커밋
                        e.Handled = true;
                    }
                }
            }
        }


        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingThumb && _activeSlider is Slider slider)
            {
                CommitSliderValue(slider, slider.Value); // 드래그 끝났을 때만 커밋
            }

            _isDraggingThumb = false;
            _isTrackClick = false;
            _isInternalChange = false;
        }


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;   // 초기 로드 방지
            if (_isInternalChange) return; // 내부 처리 중이면 무시
            if (_isDraggingThumb) return;  // 드래그 중에도 무시
            if (_isTrackClick) return;     // Track 클릭이면 이미 처리됨

            CommitSliderValue(sender as Slider, e.NewValue);
        }


        private void CommitSliderValue(Slider? slider, double value)
        {
            if (slider == null || _viewModel == null) return;

            float vol = (float)value;

            if (slider.Name == "InputLevelSlider")
            {
                _viewModel.InputVolume = vol;
                DeviceService.Instance.CommitInputVolume(vol);
            }
            else if (slider.Name == "OutputLevelSlider")
            {
                _viewModel.OutputVolume = vol;
                DeviceService.Instance.CommitOutputVolume(vol);
            }
        }




    }
}
